namespace Wasta.SupportChat.Domain;

public enum ChatRole
{
    User,
    Assistant,
}

public class ChatMessage
{
    public int Id { get; set; }
    public int SessionId { get; set; }

    /// <summary>Denormalized from ChatSession.StudentId at write time, null for
    /// anonymous visitors. Exists purely so cross-session memory (see
    /// SupportChatService) can look up "this student's recent messages" with
    /// one indexed query instead of joining through ChatSessions on every
    /// turn.</summary>
    public int? StudentId { get; set; }

    public ChatRole Role { get; set; }
    public string Content { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ChatSession? Session { get; set; }
}
