using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasta.Ai;
using Wasta.SupportChat.Data;
using Wasta.SupportChat.Domain;
using Wasta.SupportChat.Services;
using Wasta.SupportChat.Tests.Fakes;

namespace Wasta.SupportChat.Tests;

/// <summary>
/// Chat is the high-volume path - one call per message - so it should be
/// able to run a smaller, faster model than the Career Coach without the
/// two features fighting over one setting.
/// </summary>
public class ChatModelOverrideTests : IDisposable
{
    private readonly string _promptPath;
    private readonly string _knowledgePath;

    public ChatModelOverrideTests()
    {
        _promptPath = Path.Combine(Path.GetTempPath(), $"chat-prompt-{Guid.NewGuid():N}.txt");
        _knowledgePath = Path.Combine(Path.GetTempPath(), $"chat-knowledge-{Guid.NewGuid():N}.md");
        File.WriteAllText(_promptPath, "You are the Wasta support assistant. PLATFORM_KNOWLEDGE:");
        File.WriteAllText(_knowledgePath, "Students take an assessment and get a Wasta Score.");
    }

    public void Dispose()
    {
        if (File.Exists(_promptPath)) File.Delete(_promptPath);
        if (File.Exists(_knowledgePath)) File.Delete(_knowledgePath);
    }

    private SupportChatService BuildService(SupportChatDbContext db, FakeAiProvider provider, string? model)
    {
        var aiOptions = Options.Create(new AiOptions { Chain = [provider.Name] });
        var chatOptions = Options.Create(new SupportChatOptions
        {
            PromptPath = _promptPath,
            KnowledgePath = _knowledgePath,
            MinSecondsBetweenMessages = 0,
            Model = model,
            MaxTokens = 500,
            Temperature = 0.3,
        });
        var chain = new AiProviderChain([provider], aiOptions, NullLogger<AiProviderChain>.Instance);
        return new SupportChatService(db, chain, new NullJobListingProvider(), chatOptions, NullLogger<SupportChatService>.Instance);
    }

    private static SupportChatDbContext NewDb() =>
        new(new DbContextOptionsBuilder<SupportChatDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Chat_PassesItsConfiguredModel_ToTheProvider()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("An answer.");
        var service = BuildService(db, provider, model: "small-fast-model");

        var session = (await service.CreateSessionAsync(null, "v1", CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, new ChatCaller(null, "v1"), "hello", CancellationToken.None);

        Assert.Equal("small-fast-model", provider.LastCallOptions?.Model);
        Assert.Equal(500, provider.LastCallOptions?.MaxTokens);
        Assert.Equal(0.3, provider.LastCallOptions?.Temperature);
    }

    [Fact]
    public async Task Chat_LeavesModelNull_WhenNotConfigured_SoTheProviderDefaultApplies()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("An answer.");
        var service = BuildService(db, provider, model: null);

        var session = (await service.CreateSessionAsync(null, "v1", CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, new ChatCaller(null, "v1"), "hello", CancellationToken.None);

        Assert.Null(provider.LastCallOptions?.Model);
    }
}
