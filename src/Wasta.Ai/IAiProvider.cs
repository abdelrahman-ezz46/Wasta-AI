namespace Wasta.Ai;

/// <summary>One turn in a multi-turn conversation. Role is "user" or "assistant".</summary>
public sealed record AiChatTurn(string Role, string Content);

/// <summary>Per-call overrides. Null falls back to the provider's configured default
/// (AiOptions.MaxTokens / AiOptions.Temperature) - callers only set what they need to differ.</summary>
public sealed record AiCallOptions(int? MaxTokens = null, double? Temperature = null);

public interface IAiProvider
{
    string Name { get; }

    bool IsConfigured { get; }

    Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AiChatTurn> turns, AiCallOptions? callOptions, CancellationToken ct);
}

/// <summary>Thrown by <see cref="IAiProvider"/> implementations for transient failures
/// (429, 5xx, timeout) so <see cref="AiProviderChain"/> knows to fall through.</summary>
public sealed class AiTransientFailureException : Exception
{
    public AiTransientFailureException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }
}

/// <summary>Thrown when every provider in the chain has failed or is unconfigured.</summary>
public sealed class AiUnavailableException : Exception
{
    public AiUnavailableException(string message) : base(message)
    {
    }
}
