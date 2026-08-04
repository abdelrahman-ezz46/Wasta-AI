using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasta.Ai;
using Wasta.CareerCoach.Tests.Fakes;

namespace Wasta.CareerCoach.Tests;

public class AiProviderChainTests
{
    private static AiProviderChain BuildChain(params FakeAiProvider[] providers)
    {
        var options = Options.Create(new AiOptions { Chain = providers.Select(p => p.Name).ToList() });
        return new AiProviderChain(providers, options, NullLogger<AiProviderChain>.Instance);
    }

    [Fact]
    public async Task CompleteAsync_FirstProviderReturns429_FallsThroughToSecond()
    {
        var primary = new FakeAiProvider("groq").EnqueueThrow(new AiTransientFailureException("429"));
        var secondary = new FakeAiProvider("gemini").EnqueueResponse("ok from gemini");

        var chain = BuildChain(primary, secondary);

        var result = await chain.CompleteWithMetadataAsync("system", [new AiChatTurn("user", "user")], null, CancellationToken.None);

        Assert.Equal("ok from gemini", result.Content);
        Assert.Equal("gemini", result.ProviderName);
    }

    [Fact]
    public async Task CompleteAsync_UnconfiguredProvider_IsSkippedWithoutBeingCalled()
    {
        var primary = new FakeAiProvider("groq") { IsConfigured = false };
        var secondary = new FakeAiProvider("gemini").EnqueueResponse("ok from gemini");

        var chain = BuildChain(primary, secondary);

        var result = await chain.CompleteWithMetadataAsync("system", [new AiChatTurn("user", "user")], null, CancellationToken.None);

        Assert.Equal("gemini", result.ProviderName);
        Assert.Equal(0, primary.CallCount);
    }

    [Fact]
    public async Task CompleteAsync_AllProvidersFailTransiently_ThrowsAiUnavailable()
    {
        var primary = new FakeAiProvider("groq").EnqueueThrow(new AiTransientFailureException("500"));
        var secondary = new FakeAiProvider("gemini").EnqueueThrow(new AiTransientFailureException("timeout"));

        var chain = BuildChain(primary, secondary);

        await Assert.ThrowsAsync<AiUnavailableException>(
            () => chain.CompleteWithMetadataAsync("system", [new AiChatTurn("user", "user")], null, CancellationToken.None));
    }

    [Fact]
    public async Task CompleteAsync_NonRetryable4xx_DoesNotFallThrough()
    {
        var primary = new FakeAiProvider("groq").EnqueueThrow(new InvalidOperationException("400 bad request"));
        var secondary = new FakeAiProvider("gemini").EnqueueResponse("should never be reached");

        var chain = BuildChain(primary, secondary);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => chain.CompleteWithMetadataAsync("system", [new AiChatTurn("user", "user")], null, CancellationToken.None));

        Assert.Equal(0, secondary.CallCount);
    }

    [Fact]
    public void IsConfigured_FalseWhenNoProviderIsConfigured()
    {
        var primary = new FakeAiProvider("groq") { IsConfigured = false };
        var secondary = new FakeAiProvider("gemini") { IsConfigured = false };

        var chain = BuildChain(primary, secondary);

        Assert.False(chain.IsConfigured);
    }
}
