using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasta.Ai;
using Wasta.SupportChat.Data;
using Wasta.SupportChat.Domain;

namespace Wasta.SupportChat.Services;

/// <summary>
/// Answers one chat message at a time, on the request path (unlike the
/// Career Coach, there's no background job here - a live chat has to
/// reply within the HTTP request). Never throws: every failure mode has an
/// outcome and a user-facing reply, because a chat widget that errors is
/// worse than one that says "I'm having trouble right now".
/// </summary>
public class SupportChatService
{
    private readonly SupportChatDbContext _db;
    private readonly AiProviderChain _providerChain;
    private readonly IJobListingProvider _jobListingProvider;
    private readonly SupportChatOptions _options;
    private readonly ILogger<SupportChatService> _logger;

    public SupportChatService(
        SupportChatDbContext db,
        AiProviderChain providerChain,
        IJobListingProvider jobListingProvider,
        IOptions<SupportChatOptions> options,
        ILogger<SupportChatService> logger)
    {
        _db = db;
        _providerChain = providerChain;
        _jobListingProvider = jobListingProvider;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>Anonymous sessions REQUIRE a visitor id - it's what the caller
    /// later proves ownership with. Returns null if an anonymous caller
    /// supplied none, rather than creating a session nobody can reach.</summary>
    public async Task<ChatSession?> CreateSessionAsync(int? studentId, string? visitorId, CancellationToken ct)
    {
        if (studentId is null && string.IsNullOrWhiteSpace(visitorId))
        {
            return null;
        }

        var session = new ChatSession { StudentId = studentId, VisitorId = visitorId };
        _db.ChatSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<IReadOnlyList<ChatMessage>> GetHistoryAsync(Guid sessionPublicId, ChatCaller caller, CancellationToken ct)
    {
        var session = await _db.ChatSessions
            .Include(s => s.Messages)
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.PublicId == sessionPublicId, ct);

        if (session is null)
        {
            return [];
        }

        if (!caller.CanAccess(session))
        {
            _logger.LogWarning("Rejected unauthorized history read for session {SessionId}", sessionPublicId);
            return [];
        }

        return session.Messages.OrderBy(m => m.CreatedAt).ToList();
    }

    public async Task<SendMessageResult> SendMessageAsync(Guid sessionPublicId, ChatCaller caller, string? userMessage, CancellationToken ct)
    {
        var session = await _db.ChatSessions
            .Include(s => s.Messages)
            .FirstOrDefaultAsync(s => s.PublicId == sessionPublicId, ct);

        if (session is null)
        {
            return new SendMessageResult(sessionPublicId, string.Empty, ChatSendOutcome.SessionNotFound);
        }

        if (!caller.CanAccess(session))
        {
            _logger.LogWarning("Rejected unauthorized send for session {SessionId}", sessionPublicId);
            return new SendMessageResult(sessionPublicId, string.Empty, ChatSendOutcome.NotAuthorized);
        }

        var trimmed = userMessage?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            return new SendMessageResult(session.PublicId, StaticReplies.EmptyMessage, ChatSendOutcome.InvalidMessage);
        }

        if (trimmed.Length > _options.MaxMessageLength)
        {
            return new SendMessageResult(session.PublicId, StaticReplies.MessageTooLong(_options.MaxMessageLength), ChatSendOutcome.InvalidMessage);
        }

        if (session.MessageCount >= _options.MaxMessagesPerSession)
        {
            return new SendMessageResult(session.PublicId, StaticReplies.SessionLimitReached, ChatSendOutcome.SessionLimitReached);
        }

        if (session.LastMessageAt is { } last
            && (DateTimeOffset.UtcNow - last).TotalSeconds < _options.MinSecondsBetweenMessages)
        {
            return new SendMessageResult(session.PublicId, StaticReplies.RateLimited, ChatSendOutcome.RateLimited);
        }

        // Snapshot history BEFORE persisting the new user message, then build
        // the turn list explicitly - safer than relying on EF relationship
        // fixup to reflect the just-added row in session.Messages.
        var priorTurns = session.Messages.Count > 0
            ? session.Messages
                .OrderBy(m => m.CreatedAt)
                .TakeLast(_options.MaxHistoryTurns)
                .Select(ToTurn)
                .ToList()
            : await BuildCrossSessionMemoryAsync(session, ct);

        _db.ChatMessages.Add(new ChatMessage { SessionId = session.Id, StudentId = session.StudentId, Role = ChatRole.User, Content = trimmed });
        session.MessageCount += 1;
        session.LastMessageAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        var turns = new List<AiChatTurn>(priorTurns) { new("user", trimmed) };

        AiCompletionResult completion;
        try
        {
            var systemPrompt = await BuildSystemPromptAsync(session.StudentId, ct);
            completion = await _providerChain.CompleteWithMetadataAsync(
                systemPrompt, turns, new AiCallOptions(_options.MaxTokens, _options.Temperature), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Support chat completion failed for session {SessionId}", session.PublicId);
            return new SendMessageResult(session.PublicId, StaticReplies.ProviderUnavailable, ChatSendOutcome.ProviderUnavailable);
        }

        _db.ChatMessages.Add(new ChatMessage { SessionId = session.Id, StudentId = session.StudentId, Role = ChatRole.Assistant, Content = completion.Content });
        session.MessageCount += 1;
        session.LastMessageAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new SendMessageResult(session.PublicId, completion.Content, ChatSendOutcome.Answered);
    }

    /// <summary>
    /// Only called for a brand-new, empty session. If the caller is a
    /// logged-in student, pull their most recent messages from EARLIER
    /// sessions so the conversation continues rather than starting cold.
    /// Anonymous (visitor-only) sessions get nothing here - a
    /// browser-local visitor id can end up on a shared or wiped device, so
    /// it isn't a safe enough identity to carry personal conversation
    /// history across visits. This is the one query in the module that
    /// crosses session boundaries; it filters strictly by StudentId so one
    /// student's history can never surface in another student's or a
    /// visitor's session.
    /// </summary>
    private async Task<List<AiChatTurn>> BuildCrossSessionMemoryAsync(ChatSession session, CancellationToken ct)
    {
        if (session.StudentId is not { } studentId || _options.CrossSessionMemoryTurns <= 0)
        {
            return [];
        }

        var recent = await _db.ChatMessages
            .Where(m => m.StudentId == studentId && m.SessionId != session.Id)
            .OrderByDescending(m => m.CreatedAt)
            .Take(_options.CrossSessionMemoryTurns)
            .ToListAsync(ct);

        recent.Reverse();
        return recent.Select(ToTurn).ToList();
    }

    private static AiChatTurn ToTurn(ChatMessage m) => new(m.Role == ChatRole.Assistant ? "assistant" : "user", m.Content);

    private async Task<string> BuildSystemPromptAsync(int? studentId, CancellationToken ct)
    {
        var promptText = await File.ReadAllTextAsync(_options.PromptPath, ct);
        var knowledgeText = await File.ReadAllTextAsync(_options.KnowledgePath, ct);

        var prompt = new StringBuilder(promptText).Append('\n').Append(knowledgeText);

        var listings = await _jobListingProvider.GetOpenListingsAsync(studentId, _options.MaxJobListings, ct);
        if (listings.Count > 0)
        {
            prompt.Append("\n\nOPEN_OPPORTUNITIES:\n");
            foreach (var job in listings)
            {
                prompt.Append("- ").Append(job.Title).Append(" at ").Append(job.EmployerName);
                if (!string.IsNullOrWhiteSpace(job.Track))
                {
                    prompt.Append(" (").Append(job.Track).Append(')');
                }

                if (!string.IsNullOrWhiteSpace(job.Location))
                {
                    prompt.Append(", ").Append(job.Location);
                }

                if (job.Skills.Count > 0)
                {
                    prompt.Append(". Skills: ").Append(string.Join(", ", job.Skills));
                }

                prompt.Append(". Link: ").Append(job.Url).Append('\n');
            }
        }

        return prompt.ToString();
    }
}
