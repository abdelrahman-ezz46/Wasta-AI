namespace Wasta.SupportChat.Services;

public enum ChatSendOutcome
{
    Answered,
    SessionNotFound,

    /// <summary>Session exists but the caller doesn't own it. Distinct from
    /// SessionNotFound for logging only - the API deliberately reports both
    /// as 404 so callers can't probe which session ids exist.</summary>
    NotAuthorized,

    InvalidMessage,
    SessionLimitReached,
    RateLimited,
    ProviderUnavailable,
}

public sealed record SendMessageResult(Guid SessionPublicId, string Reply, ChatSendOutcome Outcome);
