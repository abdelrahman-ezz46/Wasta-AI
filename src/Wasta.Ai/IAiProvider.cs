namespace Wasta.Ai;

/// <summary>One turn in a multi-turn conversation. Role is "user" or "assistant".</summary>
public sealed record AiChatTurn(string Role, string Content);

/// <summary>
/// Per-call overrides. Null falls back to the provider's configured default
/// (AiOptions.MaxTokens / Temperature, and the provider's own Model), so
/// callers only set what they need to differ.
///
/// Model matters because features have genuinely different needs: the
/// Career Coach runs once per assessment and must emit JSON matching a
/// strict schema, so it wants a capable model; support chat runs on every
/// message and only needs short factual answers, so a smaller, faster model
/// is both cheaper and better there.
///
/// An override does NOT make a provider usable on its own - IsConfigured
/// still requires the provider's default Model to be set, so a missing
/// base configuration is caught rather than silently half-working.
/// </summary>
public sealed record AiCallOptions(int? MaxTokens = null, double? Temperature = null, string? Model = null);

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
