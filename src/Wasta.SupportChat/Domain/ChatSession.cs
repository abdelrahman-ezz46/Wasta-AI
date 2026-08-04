namespace Wasta.SupportChat.Domain;

/// <summary>
/// One conversation with the support chatbot. Works for both anonymous
/// website visitors (VisitorId, a client-generated id persisted in
/// localStorage) and logged-in students (StudentId). At least one should
/// normally be set, but neither is required - the endpoints are public.
/// </summary>
public class ChatSession
{
    public int Id { get; set; }
    public Guid PublicId { get; set; } = Guid.NewGuid();

    public int? StudentId { get; set; }
    public string? VisitorId { get; set; }

    public int MessageCount { get; set; }
    public DateTimeOffset? LastMessageAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<ChatMessage> Messages { get; set; } = [];
}
