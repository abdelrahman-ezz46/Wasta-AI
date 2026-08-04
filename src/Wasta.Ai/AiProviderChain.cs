using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Wasta.Ai;

/// <summary>
/// Tries providers in the configured order. Falls through to the next
/// provider on 429/5xx/timeout (an <see cref="AiTransientFailureException"/>).
/// Any other failure (a 4xx that isn't 429, malformed response, etc.) is our
/// bug or the request's fault, not the provider's availability, so it does
/// NOT fall through - it propagates immediately.
/// </summary>
public class AiProviderChain : IAiProvider
{
    public string Name => "chain";

    public bool IsConfigured => _orderedProviders.Any(p => p.IsConfigured);

    private readonly List<IAiProvider> _orderedProviders;
    private readonly ILogger<AiProviderChain> _logger;

    public AiProviderChain(IEnumerable<IAiProvider> providers, IOptions<AiOptions> options, ILogger<AiProviderChain> logger)
    {
        _logger = logger;
        var byName = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
        _orderedProviders = options.Value.Chain
            .Select(name => byName.GetValueOrDefault(name))
            .Where(p => p is not null)
            .Select(p => p!)
            .ToList();
    }

    public async Task<string> CompleteAsync(string systemPrompt, IReadOnlyList<AiChatTurn> turns, AiCallOptions? callOptions, CancellationToken ct)
    {
        var result = await CompleteWithMetadataAsync(systemPrompt, turns, callOptions, ct);
        return result.Content;
    }

    /// <summary>Same fallthrough behavior as <see cref="CompleteAsync"/>, but also reports
    /// which provider actually served the request, for persistence/auditing.</summary>
    public async Task<AiCompletionResult> CompleteWithMetadataAsync(
        string systemPrompt, IReadOnlyList<AiChatTurn> turns, AiCallOptions? callOptions, CancellationToken ct)
    {
        var candidates = _orderedProviders.Where(p => p.IsConfigured).ToList();
        if (candidates.Count == 0)
        {
            throw new AiUnavailableException("No AI provider is configured.");
        }

        Exception? lastTransient = null;

        foreach (var provider in candidates)
        {
            try
            {
                var result = await provider.CompleteAsync(systemPrompt, turns, callOptions, ct);
                _logger.LogInformation("AI completion served by provider {Provider}", provider.Name);
                return new AiCompletionResult(result, provider.Name);
            }
            catch (AiTransientFailureException ex)
            {
                _logger.LogWarning(ex, "Provider {Provider} failed transiently, falling through", provider.Name);
                lastTransient = ex;
            }
        }

        throw new AiUnavailableException(
            $"All configured AI providers failed. Last error: {lastTransient?.Message ?? "unknown"}");
    }
}

public sealed record AiCompletionResult(string Content, string ProviderName);
