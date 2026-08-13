using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasta.Ai;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Domain;
using Wasta.CareerCoach.Services;
using Wasta.CareerCoach.Tests.Fakes;

namespace Wasta.CareerCoach.Tests;

public class CoachModelOverrideTests : IDisposable
{
    private readonly string _promptPath;

    public CoachModelOverrideTests()
    {
        _promptPath = Path.Combine(Path.GetTempPath(), $"coach-prompt-{Guid.NewGuid():N}.txt");
        File.WriteAllText(_promptPath, "You are a technical mentor. Output raw JSON only.");
    }

    public void Dispose()
    {
        if (File.Exists(_promptPath)) File.Delete(_promptPath);
    }

    private CoachGenerationService BuildService(CoachDbContext db, FakeAiProvider provider, string? model)
    {
        var aiOptions = Options.Create(new AiOptions { Enabled = true, Chain = [provider.Name] });
        var coachOptions = Options.Create(new CoachOptions { PromptPath = _promptPath, Model = model });
        var chain = new AiProviderChain([provider], aiOptions, NullLogger<AiProviderChain>.Instance);
        var dataProvider = new FakeAssessmentDataProvider().WithAttempt(
            new AttemptScoreData(1, 1, 100, "Data & AI", [new SectionScoreData("SQL & data pipelines", 34)]));

        return new CoachGenerationService(db, dataProvider, chain, aiOptions, coachOptions, NullLogger<CoachGenerationService>.Instance);
    }

    private static CoachDbContext NewDb() =>
        new(new DbContextOptionsBuilder<CoachDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task Coach_PassesItsConfiguredModel_ToTheProvider()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse(SampleResponses.ValidJson());
        var service = BuildService(db, provider, model: "large-capable-model");

        await service.GenerateAsync(1, CancellationToken.None);

        Assert.Equal("large-capable-model", provider.LastCallOptions?.Model);
    }

    [Fact]
    public async Task Coach_LeavesModelNull_WhenNotConfigured_SoTheProviderDefaultApplies()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse(SampleResponses.ValidJson());
        var service = BuildService(db, provider, model: null);

        await service.GenerateAsync(1, CancellationToken.None);

        Assert.Null(provider.LastCallOptions?.Model);
    }

    [Fact]
    public async Task Coach_DoesNotOverrideMaxTokensOrTemperature_LeavingTheGlobalDefaults()
    {
        // The coach writes a long structured plan, so it deliberately keeps
        // the generous global MaxTokens rather than chat's smaller budget.
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse(SampleResponses.ValidJson());
        var service = BuildService(db, provider, model: "large-capable-model");

        await service.GenerateAsync(1, CancellationToken.None);

        Assert.Null(provider.LastCallOptions?.MaxTokens);
        Assert.Null(provider.LastCallOptions?.Temperature);
    }
}
