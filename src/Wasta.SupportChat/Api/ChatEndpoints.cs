using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Wasta.SupportChat.Domain;
using Wasta.SupportChat.Services;

namespace Wasta.SupportChat.Api;

/// <summary>
/// Public endpoints (no auth required - a visitor should be able to ask
/// "how does scoring work" without logging in). Abuse resistance lives in
/// SupportChatService (session cap, throttle, message length), not here.
/// </summary>
public static class ChatEndpoints
{
    private const int MaxVisitorIdLength = 64;

    public static IEndpointRouteBuilder MapSupportChatEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/chat/sessions", CreateSessionAsync);
        app.MapPost("/api/chat/sessions/{sessionId:guid}/messages", SendMessageAsync);
        app.MapGet("/api/chat/sessions/{sessionId:guid}/messages", GetHistoryAsync);

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
        var visitorId = request.VisitorId;
        if (!string.IsNullOrEmpty(visitorId) && visitorId.Length > MaxVisitorIdLength)
        {
            visitorId = visitorId[..MaxVisitorIdLength];
        }

        var session = await chatService.CreateSessionAsync(studentId, visitorId, ct);

        return Results.Ok(new CreateSessionResponse { SessionId = session.PublicId });
    }

    internal static async Task<IResult> SendMessageAsync(
        Guid sessionId,
        SendMessageRequest request,
        SupportChatService chatService,
        CancellationToken ct)
    {
        var result = await chatService.SendMessageAsync(sessionId, request.Message, ct);

        if (result.Outcome == ChatSendOutcome.SessionNotFound)
        {
            return Results.NotFound();
        }

        return Results.Ok(new SendMessageResponse
        {
            Outcome = OutcomeToString(result.Outcome),
            Reply = result.Reply,
        });
    }

    internal static async Task<IResult> GetHistoryAsync(Guid sessionId, SupportChatService chatService, CancellationToken ct)
    {
        var history = await chatService.GetHistoryAsync(sessionId, ct);

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
