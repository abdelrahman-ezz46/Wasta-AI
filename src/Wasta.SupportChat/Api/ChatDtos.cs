using System.Text.Json.Serialization;

namespace Wasta.SupportChat.Api;

public sealed class CreateSessionRequest
{
    [JsonPropertyName("visitorId")]
    public string? VisitorId { get; set; }
}

public sealed class CreateSessionResponse
{
    [JsonPropertyName("sessionId")]
    public required Guid SessionId { get; init; }
}

public sealed class SendMessageRequest
{
    [JsonPropertyName("message")]
    public string? Message { get; set; }
}

public sealed class SendMessageResponse
{
    [JsonPropertyName("outcome")]
    public required string Outcome { get; init; }

    [JsonPropertyName("reply")]
    public required string Reply { get; init; }
}

public sealed class ChatMessageResponse
{
    [JsonPropertyName("role")]
    public required string Role { get; init; }

    [JsonPropertyName("content")]
    public required string Content { get; init; }

    [JsonPropertyName("createdAt")]
    public required DateTimeOffset CreatedAt { get; init; }
}
