namespace Wasta.SupportChat.Services;

/// <summary>Canned replies for cases that never touch the AI - either because we
/// deliberately declined to call it (rate limit, session cap) or because it
/// failed. These are never stored as ChatMessage rows; they're not really
/// "from the assistant", they're the widget explaining what happened.</summary>
internal static class StaticReplies
{
    public const string ProviderUnavailable =
        "I'm having trouble answering right now. Please try again in a moment, or contact support if this keeps happening.";

    public const string SessionLimitReached =
        "This conversation has reached its length limit. Please start a new chat, or contact support for further help.";

    public const string RateLimited =
        "You're sending messages a little fast - please wait a moment before sending another.";

    public const string EmptyMessage = "Type a question and I'll try to help.";

    public static string MessageTooLong(int maxLength) =>
        $"That message is longer than {maxLength} characters. Try asking in a shorter message.";
}
