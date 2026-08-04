using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Wasta.CareerCoach.BackgroundJobs;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Domain;
using Wasta.CareerCoach.Services;

namespace Wasta.CareerCoach.Tests;

public class CoachPlanTriggerTests
{
    private static CoachDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CoachDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachDbContext(options);
    }

    [Fact]
    public async Task EnqueueGenerationAsync_ReturnsUnderThreeSeconds_WithRowLeftPending()
    {
        // Proves the submit path never awaits generation: this only inserts
        // a Pending row and hands the attempt id to the queue - no AI
        // provider call happens on this path at all.
        using var db = NewDb();
        var queue = new CoachGenerationQueue();
        var trigger = new CoachPlanTrigger(db, queue);

        var stopwatch = Stopwatch.StartNew();
        await trigger.EnqueueGenerationAsync(studentId: 1, attemptId: 42, scoreId: 100, CancellationToken.None);
        stopwatch.Stop();

        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(3), $"Took {stopwatch.Elapsed}");

        var plan = await db.StudentCoachPlans.SingleAsync(p => p.AttemptId == 42);
        Assert.Equal(CoachStatus.Pending, plan.Status);
    }

    [Fact]
    public async Task EnqueueGenerationAsync_CalledTwice_DoesNotDuplicateRow()
    {
        using var db = NewDb();
        var queue = new CoachGenerationQueue();
        var trigger = new CoachPlanTrigger(db, queue);

        await trigger.EnqueueGenerationAsync(1, 42, 100, CancellationToken.None);
        await trigger.EnqueueGenerationAsync(1, 42, 100, CancellationToken.None);

        Assert.Equal(1, await db.StudentCoachPlans.CountAsync(p => p.AttemptId == 42));
    }
}
