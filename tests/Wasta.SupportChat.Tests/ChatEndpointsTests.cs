using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasta.Ai;
using Wasta.SupportChat.Api;
using Wasta.SupportChat.Data;
using Wasta.SupportChat.Domain;
using Wasta.SupportChat.Services;
using Wasta.SupportChat.Tests.Fakes;

namespace Wasta.SupportChat.Tests;

public class ChatEndpointsTests : IDisposable
{
    private const string OwnerVisitorId = "owner-visitor";
    private const string AttackerVisitorId = "attacker-visitor";

    private readonly string _promptPath;
    private readonly string _knowledgePath;

    public ChatEndpointsTests()
    {
        _promptPath = Path.Combine(Path.GetTempPath(), $"chat-prompt-{Guid.NewGuid():N}.txt");
        _knowledgePath = Path.Combine(Path.GetTempPath(), $"chat-knowledge-{Guid.NewGuid():N}.md");
        File.WriteAllText(_promptPath, "You are the Wasta support assistant. PLATFORM_KNOWLEDGE:");
        File.WriteAllText(_knowledgePath, "Students take an assessment and get a Wasta Score.");
    }

    public void Dispose()
    {
        if (File.Exists(_promptPath)) File.Delete(_promptPath);
        if (File.Exists(_knowledgePath)) File.Delete(_knowledgePath);
    }

    private static SupportChatDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<SupportChatDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SupportChatDbContext(options);
    }

    private SupportChatService BuildService(SupportChatDbContext db, params FakeAiProvider[] providers)
    {
        var aiOptions = Options.Create(new AiOptions { Chain = providers.Select(p => p.Name).ToList() });
        var chatOptions = Options.Create(new SupportChatOptions
        {
            PromptPath = _promptPath,
            KnowledgePath = _knowledgePath,
            MinSecondsBetweenMessages = 0,
        });
        var chain = new AiProviderChain(providers, aiOptions, NullLogger<AiProviderChain>.Instance);
        return new SupportChatService(db, chain, new NullJobListingProvider(), chatOptions, NullLogger<SupportChatService>.Instance);
    }

    private static ClaimsPrincipal AnyUser() => new(new ClaimsIdentity());

    private static HttpContext ContextFor(string? visitorId)
    {
        var context = new DefaultHttpContext();
        if (visitorId is not null)
        {
            context.Request.Headers[ChatEndpoints.VisitorIdHeader] = visitorId;
        }

        return context;
    }

    private static FakeCurrentStudentAccessor AsStudent(int? id) => new(id);

    [Fact]
    public async Task CreateSession_ReturnsNewSessionId()
    {
        using var db = NewDb();
        var service = BuildService(db, new FakeAiProvider("groq"));

        var result = await ChatEndpoints.CreateSessionAsync(
            new CreateSessionRequest { VisitorId = OwnerVisitorId },
            AnyUser(), AsStudent(null), service, CancellationToken.None);

        var ok = Assert.IsType<Ok<CreateSessionResponse>>(result);
        Assert.NotEqual(Guid.Empty, ok.Value!.SessionId);
    }

    [Fact]
    public async Task CreateSession_AnonymousWithoutVisitorId_IsRejected()
    {
        using var db = NewDb();
        var service = BuildService(db, new FakeAiProvider("groq"));

        var result = await ChatEndpoints.CreateSessionAsync(
            new CreateSessionRequest { VisitorId = null },
            AnyUser(), AsStudent(null), service, CancellationToken.None);

        // A session nobody can prove ownership of would be unreachable, so
        // it's refused at creation rather than created and orphaned.
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, statusResult.StatusCode);
        Assert.Empty(await db.ChatSessions.ToListAsync());
    }

    [Fact]
    public async Task SendMessage_UnknownSession_Returns404()
    {
        using var db = NewDb();
        var service = BuildService(db, new FakeAiProvider("groq"));

        var result = await ChatEndpoints.SendMessageAsync(
            Guid.NewGuid(), new SendMessageRequest { Message = "hi" },
            ContextFor(OwnerVisitorId), AnyUser(), AsStudent(null), service, CancellationToken.None);

        Assert.IsType<NotFound>(result);
    }

    [Fact]
    public async Task SendMessage_KnownSession_ReturnsAnsweredOutcomeAndReply()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("Here's how scoring works.");
        var service = BuildService(db, provider);

        var session = (await service.CreateSessionAsync(null, OwnerVisitorId, CancellationToken.None))!;
        var result = await ChatEndpoints.SendMessageAsync(
            session.PublicId, new SendMessageRequest { Message = "How does scoring work?" },
            ContextFor(OwnerVisitorId), AnyUser(), AsStudent(null), service, CancellationToken.None);

        var ok = Assert.IsType<Ok<SendMessageResponse>>(result);
        Assert.Equal("answered", ok.Value!.Outcome);
        Assert.Equal("Here's how scoring works.", ok.Value.Reply);
    }

    [Fact]
    public async Task GetHistory_ReturnsMessagesMappedToLowercaseRoles()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("An answer.");
        var service = BuildService(db, provider);

        var session = (await service.CreateSessionAsync(null, OwnerVisitorId, CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, new ChatCaller(null, OwnerVisitorId), "A question", CancellationToken.None);

        var result = await ChatEndpoints.GetHistoryAsync(
            session.PublicId, ContextFor(OwnerVisitorId), AnyUser(), AsStudent(null), service, CancellationToken.None);

        var ok = Assert.IsType<Ok<List<ChatMessageResponse>>>(result);
        Assert.Equal(2, ok.Value!.Count);
        Assert.Equal("user", ok.Value[0].Role);
        Assert.Equal("assistant", ok.Value[1].Role);
    }

    [Fact]
    public async Task GetHistory_UnknownSession_ReturnsEmptyListNotError()
    {
        using var db = NewDb();
        var service = BuildService(db, new FakeAiProvider("groq"));

        var result = await ChatEndpoints.GetHistoryAsync(
            Guid.NewGuid(), ContextFor(OwnerVisitorId), AnyUser(), AsStudent(null), service, CancellationToken.None);

        var ok = Assert.IsType<Ok<List<ChatMessageResponse>>>(result);
        Assert.Empty(ok.Value!);
    }

    // --- Authorization: a session id alone must never be enough ---

    [Fact]
    public async Task GetHistory_WithStolenSessionId_ButWrongVisitor_LeaksNothing()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("An answer.");
        var service = BuildService(db, provider);

        var session = (await service.CreateSessionAsync(null, OwnerVisitorId, CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, new ChatCaller(null, OwnerVisitorId), "A private question", CancellationToken.None);

        var result = await ChatEndpoints.GetHistoryAsync(
            session.PublicId, ContextFor(AttackerVisitorId), AnyUser(), AsStudent(null), service, CancellationToken.None);

        var ok = Assert.IsType<Ok<List<ChatMessageResponse>>>(result);
        Assert.Empty(ok.Value!);
    }

    [Fact]
    public async Task SendMessage_WithStolenSessionId_ButWrongVisitor_Returns404AndNeverCallsAi()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq");
        var service = BuildService(db, provider);

        var session = (await service.CreateSessionAsync(null, OwnerVisitorId, CancellationToken.None))!;

        var result = await ChatEndpoints.SendMessageAsync(
            session.PublicId, new SendMessageRequest { Message = "let me in" },
            ContextFor(AttackerVisitorId), AnyUser(), AsStudent(null), service, CancellationToken.None);

        // 404 rather than 403: a distinct status would confirm the session id
        // is real, turning the endpoint into a session-id oracle.
        Assert.IsType<NotFound>(result);
        Assert.Equal(0, provider.CallCount);
    }

    [Fact]
    public async Task GetHistory_StudentSession_IsNotReadableByAnotherStudent()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("An answer.");
        var service = BuildService(db, provider);

        var session = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, new ChatCaller(1, null), "Student 1's question", CancellationToken.None);

        var result = await ChatEndpoints.GetHistoryAsync(
            session.PublicId, ContextFor(null), AnyUser(), AsStudent(2), service, CancellationToken.None);

        var ok = Assert.IsType<Ok<List<ChatMessageResponse>>>(result);
        Assert.Empty(ok.Value!);
    }

    [Fact]
    public async Task GetHistory_StudentSession_IsNotReadableByAnonymousCallerHoldingTheSessionId()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("An answer.");
        var service = BuildService(db, provider);

        var session = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, new ChatCaller(1, null), "Student 1's question", CancellationToken.None);

        var result = await ChatEndpoints.GetHistoryAsync(
            session.PublicId, ContextFor(AttackerVisitorId), AnyUser(), AsStudent(null), service, CancellationToken.None);

        var ok = Assert.IsType<Ok<List<ChatMessageResponse>>>(result);
        Assert.Empty(ok.Value!);
    }

    [Fact]
    public async Task SendMessage_StudentSession_CannotBeContinuedByAnotherStudent()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq");
        var service = BuildService(db, provider);

        var session = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;

        var result = await ChatEndpoints.SendMessageAsync(
            session.PublicId, new SendMessageRequest { Message = "continue as student 1" },
            ContextFor(null), AnyUser(), AsStudent(2), service, CancellationToken.None);

        // Critical: continuing someone's session would pull THEIR cross-visit
        // memory into the prompt, so this is a data-exposure path, not just
        // an impersonation one.
        Assert.IsType<NotFound>(result);
        Assert.Equal(0, provider.CallCount);
    }
}
