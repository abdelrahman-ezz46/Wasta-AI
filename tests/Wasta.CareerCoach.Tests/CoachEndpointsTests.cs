using System.Security.Claims;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Wasta.CareerCoach.Api;
using Wasta.CareerCoach.BackgroundJobs;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Domain;
using Wasta.CareerCoach.Tests.Fakes;

namespace Wasta.CareerCoach.Tests;

public class CoachEndpointsTests
{
    private static CoachDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<CoachDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachDbContext(options);
    }

    private static ClaimsPrincipal AnyUser() => new(new ClaimsIdentity());

    [Fact]
    public async Task GetMyCoachPlan_WhenNoPlanExists_ReturnsUnavailableNot404()
    {
        using var db = NewDb();
        var dataProvider = new FakeAssessmentDataProvider().WithAttempt(
            new AttemptScoreData(1, 1, 1, "Data & AI", []));

        var result = await CoachEndpoints.GetMyCoachPlanAsync(
            AnyUser(), new FakeCurrentStudentAccessor(1), dataProvider, db, CancellationToken.None);

        var ok = Assert.IsType<Ok<CoachPlanUnavailableBody>>(result);
        Assert.Equal("unavailable", ok.Value!.Status);
    }

    [Fact]
    public async Task GetMyCoachPlan_WhenPending_ReturnsPending()
    {
        using var db = NewDb();
        db.StudentCoachPlans.Add(new StudentCoachPlan { AttemptId = 1, StudentId = 1, Status = CoachStatus.Pending });
        await db.SaveChangesAsync();

        var dataProvider = new FakeAssessmentDataProvider().WithAttempt(
            new AttemptScoreData(1, 1, 1, "Data & AI", []));

        var result = await CoachEndpoints.GetMyCoachPlanAsync(
            AnyUser(), new FakeCurrentStudentAccessor(1), dataProvider, db, CancellationToken.None);

        var ok = Assert.IsType<Ok<CoachPlanPendingBody>>(result);
        Assert.Equal("pending", ok.Value!.Status);
    }

    [Fact]
    public async Task GetMyCoachPlan_WhenFailed_ReturnsUnavailableNeverAnError()
    {
        using var db = NewDb();
        db.StudentCoachPlans.Add(new StudentCoachPlan
        {
            AttemptId = 1,
            StudentId = 1,
            Status = CoachStatus.Failed,
            AttemptCount = 3,
            LastError = "All providers failed",
        });
        await db.SaveChangesAsync();

        var dataProvider = new FakeAssessmentDataProvider().WithAttempt(
            new AttemptScoreData(1, 1, 1, "Data & AI", []));

        var result = await CoachEndpoints.GetMyCoachPlanAsync(
            AnyUser(), new FakeCurrentStudentAccessor(1), dataProvider, db, CancellationToken.None);

        var ok = Assert.IsType<Ok<CoachPlanUnavailableBody>>(result);
        Assert.Equal("unavailable", ok.Value!.Status);
    }

    [Fact]
    public async Task GetMyCoachPlan_WhenReady_ReturnsFullPlanBody()
    {
        using var db = NewDb();
        db.StudentCoachPlans.Add(new StudentCoachPlan
        {
            AttemptId = 1,
            StudentId = 1,
            Status = CoachStatus.Ready,
            Headline = "Test headline",
            Assessment = "Test assessment",
            WeeklyPlanJson = "[]",
            ProjectTitle = "Test project",
            ProjectDesc = "Test description",
            ProjectSkillsJson = """["SQL"]""",
            InterviewLine = "Test interview line",
        });
        await db.SaveChangesAsync();

        var dataProvider = new FakeAssessmentDataProvider().WithAttempt(
            new AttemptScoreData(1, 1, 1, "Data & AI", []));

        var result = await CoachEndpoints.GetMyCoachPlanAsync(
            AnyUser(), new FakeCurrentStudentAccessor(1), dataProvider, db, CancellationToken.None);

        var ok = Assert.IsType<Ok<CoachPlanReadyBody>>(result);
        Assert.Equal("ready", ok.Value!.Status);
        Assert.Equal("Test headline", ok.Value.Headline);
        Assert.Equal(["SQL"], ok.Value.ProjectSuggestion.SkillsPractised);
    }

    [Fact]
    public async Task Regenerate_ResetsAttemptCountAndEnqueuesAndAudits()
    {
        using var db = NewDb();
        db.StudentCoachPlans.Add(new StudentCoachPlan
        {
            AttemptId = 5,
            StudentId = 1,
            Status = CoachStatus.Failed,
            AttemptCount = 3,
            LastError = "boom",
        });
        await db.SaveChangesAsync();

        var queue = new CoachGenerationQueue();
        var audit = new FakeAuditLogWriter();

        var result = await CoachEndpoints.RegenerateAsync(5, AnyUser(), db, queue, audit, CancellationToken.None);

        Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IResult>(result);

        var reloaded = await db.StudentCoachPlans.SingleAsync(p => p.AttemptId == 5);
        Assert.Equal(0, reloaded.AttemptCount);
        Assert.Equal(CoachStatus.Pending, reloaded.Status);
        Assert.Null(reloaded.LastError);

        Assert.Single(audit.Entries);
        Assert.Equal("coach_plan.regenerate", audit.Entries[0].Action);
    }
}
