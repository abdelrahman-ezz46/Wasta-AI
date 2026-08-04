using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasta.Ai;
using Wasta.CareerCoach.BackgroundJobs;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Domain;
using Wasta.CareerCoach.Services;
using Wasta.CareerCoach.Tests.Fakes;

namespace Wasta.CareerCoach.Tests;

/// <summary>
/// The sweeper is the only recovery path for a plan that never generated,
/// and it had no coverage at all. These tests pin the two things it must
/// do - retry Failed rows within the attempt cap, and rescue rows abandoned
/// in Pending - and the two it must not: exhausted rows and in-flight ones.
/// </summary>
public class CoachSweeperServiceTests : IDisposable
{
    private readonly string _promptPath;
    private readonly ServiceProvider _provider;
    private readonly CoachDbContext _db;
    private readonly FakeAiProvider _ai;

    public CoachSweeperServiceTests()
    {
        _promptPath = Path.Combine(Path.GetTempPath(), $"sweeper-prompt-{Guid.NewGuid():N}.txt");
        File.WriteAllText(_promptPath, "You are a technical mentor. Output raw JSON only.");

        _ai = new FakeAiProvider("groq");
        var databaseName = Guid.NewGuid().ToString();

        var services = new ServiceCollection();
        services.AddSingleton<IAiProvider>(_ai);
        services.AddSingleton(Options.Create(new AiOptions { Enabled = true, Chain = ["groq"] }));
        services.AddSingleton(Options.Create(new CoachOptions { PromptPath = _promptPath }));
        services.AddSingleton<AiProviderChain>();
        services.AddSingleton(NullLoggerFactory.Instance);
        services.AddLogging();
        services.AddDbContext<CoachDbContext>(o => o.UseInMemoryDatabase(databaseName));
        services.AddScoped<IAssessmentDataProvider>(_ => new FakeAssessmentDataProvider()
            .WithAttempt(new AttemptScoreData(1, 1, 100, "Data & AI",
                [new SectionScoreData("SQL & data pipelines", 34)]))
            .WithAttempt(new AttemptScoreData(1, 2, 101, "Data & AI",
                [new SectionScoreData("SQL & data pipelines", 34)])));
        services.AddScoped<CoachGenerationService>();

        _provider = services.BuildServiceProvider();
        _db = _provider.GetRequiredService<CoachDbContext>();
    }

    public void Dispose()
    {
        _provider.Dispose();
        if (File.Exists(_promptPath)) File.Delete(_promptPath);
    }

    private CoachSweeperService NewSweeper() => new(
        _provider.GetRequiredService<IServiceScopeFactory>(),
        NullLogger<CoachSweeperService>.Instance);

    private static DateTimeOffset Stale => DateTimeOffset.UtcNow - CoachSweeperService.StalePendingAfter - TimeSpan.FromMinutes(1);

    [Fact]
    public async Task Sweep_RetriesFailedPlanUnderAttemptCap()
    {
        _db.StudentCoachPlans.Add(new StudentCoachPlan
        {
            AttemptId = 1, StudentId = 1, ScoreId = 100,
            Status = CoachStatus.Failed, AttemptCount = 1,
        });
        await _db.SaveChangesAsync();
        _ai.EnqueueResponse(SampleResponses.ValidJson());

        await NewSweeper().SweepAsync(CancellationToken.None);

        var plan = await _db.StudentCoachPlans.AsNoTracking().SingleAsync(p => p.AttemptId == 1);
        Assert.Equal(CoachStatus.Ready, plan.Status);
    }

    [Fact]
    public async Task Sweep_SkipsFailedPlanThatExhaustedItsAttempts()
    {
        _db.StudentCoachPlans.Add(new StudentCoachPlan
        {
            AttemptId = 1, StudentId = 1, ScoreId = 100,
            Status = CoachStatus.Failed, AttemptCount = 3,
        });
        await _db.SaveChangesAsync();

        await NewSweeper().SweepAsync(CancellationToken.None);

        Assert.Equal(0, _ai.CallCount);
        var plan = await _db.StudentCoachPlans.AsNoTracking().SingleAsync(p => p.AttemptId == 1);
        Assert.Equal(CoachStatus.Failed, plan.Status);
    }

    [Fact]
    public async Task Sweep_RescuesStalePendingPlan_TheQueueFullAndRestartCase()
    {
        // A row stuck Pending because the queue was full at submit time, or
        // the process restarted before the worker got to it. Nothing else
        // ever revisits these, so without the sweeper the student's coach
        // card would be permanently missing.
        _db.StudentCoachPlans.Add(new StudentCoachPlan
        {
            AttemptId = 1, StudentId = 1, ScoreId = 100,
            Status = CoachStatus.Pending, CreatedAt = Stale,
        });
        await _db.SaveChangesAsync();
        _ai.EnqueueResponse(SampleResponses.ValidJson());

        await NewSweeper().SweepAsync(CancellationToken.None);

        var plan = await _db.StudentCoachPlans.AsNoTracking().SingleAsync(p => p.AttemptId == 1);
        Assert.Equal(CoachStatus.Ready, plan.Status);
    }

    [Fact]
    public async Task Sweep_LeavesRecentPendingPlanAlone_SoItDoesNotRaceInFlightGeneration()
    {
        _db.StudentCoachPlans.Add(new StudentCoachPlan
        {
            AttemptId = 1, StudentId = 1, ScoreId = 100,
            Status = CoachStatus.Pending, CreatedAt = DateTimeOffset.UtcNow,
        });
        await _db.SaveChangesAsync();

        await NewSweeper().SweepAsync(CancellationToken.None);

        Assert.Equal(0, _ai.CallCount);
        var plan = await _db.StudentCoachPlans.AsNoTracking().SingleAsync(p => p.AttemptId == 1);
        Assert.Equal(CoachStatus.Pending, plan.Status);
    }

    [Fact]
    public async Task Sweep_LeavesReadyPlansUntouched()
    {
        _db.StudentCoachPlans.Add(new StudentCoachPlan
        {
            AttemptId = 1, StudentId = 1, ScoreId = 100,
            Status = CoachStatus.Ready, Headline = "Existing headline", CreatedAt = Stale,
        });
        await _db.SaveChangesAsync();

        await NewSweeper().SweepAsync(CancellationToken.None);

        Assert.Equal(0, _ai.CallCount);
        var plan = await _db.StudentCoachPlans.AsNoTracking().SingleAsync(p => p.AttemptId == 1);
        Assert.Equal("Existing headline", plan.Headline);
    }
}
