using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Wasta.SupportChat.Domain;
using Wasta.SupportChat.Services;

namespace Wasta.SupportChat.Api;

/// <summary>
/// Public endpoints - a visitor should be able to ask "how does scoring
/// work" without logging in. Because they're public, they carry their own
/// protection: per-IP rate limits (see SupportChatRateLimiting) against
/// cost abuse, and per-caller ownership checks (see ChatCaller) so a leaked
/// session id isn't enough to read or continue someone's conversation.
/// </summary>
public static class ChatEndpoints
{
    private const int MaxVisitorIdLength = 64;

    /// <summary>Anonymous callers echo back the visitor id they were created
    /// with to prove session ownership. A header rather than a query
    /// parameter so it stays out of URLs, and therefore out of access logs
    /// and browser history.</summary>
    public const string VisitorIdHeader = "X-Wasta-Visitor-Id";

    public static IEndpointRouteBuilder MapSupportChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat/sessions", CreateSessionAsync)
            .RequireRateLimiting(SupportChatRateLimiting.SessionCreationPolicy);

        app.MapPost("/api/chat/sessions/{sessionId:guid}/messages", SendMessageAsync)
            .RequireRateLimiting(SupportChatRateLimiting.MessagePolicy);

        app.MapGet("/api/chat/sessions/{sessionId:guid}/messages", GetHistoryAsync)
            .RequireRateLimiting(SupportChatRateLimiting.MessagePolicy);

        return app;
    }

    internal static async Task<IResult> CreateSessionAsync(
        CreateSessionRequest request,
        ClaimsPrincipal user,
        ICurrentStudentAccessor currentStudentAccessor,
        SupportChatService chatService,
        CancellationToken ct)
    {
        var studentId = currentStudentAccessor.GetStudentId(user);
        var visitorId = Truncate(request.VisitorId);

        var session = await chatService.CreateSessionAsync(studentId, visitorId, ct);
        if (session is null)
        {
            return Results.BadRequest(new { error = "A visitorId is required for anonymous chat sessions." });
        }

        return Results.Ok(new CreateSessionResponse { SessionId = session.PublicId });
    }

    internal static async Task<IResult> SendMessageAsync(
        Guid sessionId,
        SendMessageRequest request,
        HttpContext httpContext,
        ClaimsPrincipal user,
        ICurrentStudentAccessor currentStudentAccessor,
        SupportChatService chatService,
        CancellationToken ct)
    {
        var caller = ResolveCaller(httpContext, user, currentStudentAccessor);
        var result = await chatService.SendMessageAsync(sessionId, caller, request.Message, ct);

        // Unauthorized deliberately reports as 404, identical to a session
        // that doesn't exist, so the API can't be used to enumerate valid
        // session ids.
        if (result.Outcome is ChatSendOutcome.SessionNotFound or ChatSendOutcome.NotAuthorized)
        {
            return Results.NotFound();
        }

        return Results.Ok(new SendMessageResponse
        {
            Outcome = OutcomeToString(result.Outcome),
            Reply = result.Reply,
        });
    }

    internal static async Task<IResult> GetHistoryAsync(
        Guid sessionId,
        HttpContext httpContext,
        ClaimsPrincipal user,
        ICurrentStudentAccessor currentStudentAccessor,
        SupportChatService chatService,
        CancellationToken ct)
    {
        var caller = ResolveCaller(httpContext, user, currentStudentAccessor);
        var history = await chatService.GetHistoryAsync(sessionId, caller, ct);

        var body = history
            .Select(m => new ChatMessageResponse
            {
                Role = m.Role.ToString().ToLowerInvariant(),
                Content = m.Content,
                CreatedAt = m.CreatedAt,
            })
            .ToList();

        return Results.Ok(body);
    }

    private static ChatCaller ResolveCaller(
        HttpContext httpContext, ClaimsPrincipal user, ICurrentStudentAccessor currentStudentAccessor)
    {
        var studentId = currentStudentAccessor.GetStudentId(user);
        var visitorId = Truncate(httpContext.Request.Headers[VisitorIdHeader].FirstOrDefault());
        return new ChatCaller(studentId, visitorId);
    }

    private static string? Truncate(string? value)
        => string.IsNullOrEmpty(value) || value.Length <= MaxVisitorIdLength ? value : value[..MaxVisitorIdLength];

    private static string OutcomeToString(ChatSendOutcome outcome) => outcome switch
    {
        ChatSendOutcome.Answered => "answered",
        ChatSendOutcome.InvalidMessage => "invalid_message",
        ChatSendOutcome.SessionLimitReached => "session_limit_reached",
        ChatSendOutcome.RateLimited => "rate_limited",
        ChatSendOutcome.ProviderUnavailable => "provider_unavailable",
        _ => "unknown",
    };
}
