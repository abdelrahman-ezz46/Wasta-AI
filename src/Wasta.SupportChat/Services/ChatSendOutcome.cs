namespace Wasta.SupportChat.Services;

public enum ChatSendOutcome
{
    Answered,
    SessionNotFound,
    InvalidMessage,
    SessionLimitReached,
    RateLimited,
    ProviderUnavailable,
}

public sealed record SendMessageResult(Guid SessionPublicId, string Reply, ChatSendOutcome Outcome);
