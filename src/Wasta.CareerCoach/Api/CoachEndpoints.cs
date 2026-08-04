using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Wasta.CareerCoach.BackgroundJobs;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Domain;
using Wasta.CareerCoach.Services;

namespace Wasta.CareerCoach.Api;

/// <summary>
/// Maps the three Career Coach endpoints. The student endpoint never
/// returns 404 or 500 for a missing/failed plan - it always resolves to one
/// of pending/ready/unavailable, because a broken or absent plan must never
/// break the results page it's embedded in.
/// </summary>
public static class CoachEndpoints
{
    public static IEndpointRouteBuilder MapCareerCoachEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/students/me/coach-plan", GetMyCoachPlanAsync)
            .RequireAuthorization("StudentOnly");

        app.MapPost("/api/admin/coach-plans/{attemptId:int}/regenerate", RegenerateAsync)
            .RequireAuthorization("AdminOnly");

        app.MapGet("/api/admin/coach-plans/stats", GetStatsAsync)
            .RequireAuthorization("AdminOnly");

        return app;
    }

    internal static async Task<IResult> GetMyCoachPlanAsync(
        ClaimsPrincipal user,
        ICurrentStudentAccessor currentStudentAccessor,
        IAssessmentDataProvider dataProvider,
        CoachDbContext db,
        CancellationToken ct)
    {
        var studentId = currentStudentAccessor.GetStudentId(user);
        if (studentId is null)
        {
            return Results.Ok(new CoachPlanUnavailableBody());
        }

        var attemptId = await dataProvider.GetCurrentAttemptIdAsync(studentId.Value, ct);
        if (attemptId is null)
        {
            return Results.Ok(new CoachPlanUnavailableBody());
        }

        var plan = await db.StudentCoachPlans.AsNoTracking().FirstOrDefaultAsync(p => p.AttemptId == attemptId.Value, ct);

        return plan switch
        {
            null => Results.Ok(new CoachPlanUnavailableBody()),
            { Status: CoachStatus.Pending } => Results.Ok(new CoachPlanPendingBody()),
            { Status: CoachStatus.Ready } => Results.Ok(ToReadyBody(plan)),
            _ => Results.Ok(new CoachPlanUnavailableBody()),
        };
    }

    internal static async Task<IResult> RegenerateAsync(
        int attemptId,
        ClaimsPrincipal user,
        CoachDbContext db,
        CoachGenerationQueue queue,
        IAuditLogWriter auditLog,
        CancellationToken ct)
    {
        var plan = await db.StudentCoachPlans.FirstOrDefaultAsync(p => p.AttemptId == attemptId, ct);
        if (plan is null)
        {
            plan = new StudentCoachPlan { AttemptId = attemptId, Status = CoachStatus.Pending };
            db.StudentCoachPlans.Add(plan);
        }
        else
        {
            plan.AttemptCount = 0;
            plan.Status = CoachStatus.Pending;
            plan.LastError = null;
        }

        await db.SaveChangesAsync(ct);

        var actorId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        await auditLog.WriteAsync("coach_plan.regenerate", actorId, $"attemptId={attemptId}", ct);

        if (!queue.TryEnqueue(attemptId))
        {
            // Queue full: row stays Pending, CoachSweeperService will pick it up.
        }

        return Results.Ok(new { status = "queued", attemptId });
    }

    internal static async Task<IResult> GetStatsAsync(CoachDbContext db, CancellationToken ct)
    {
        var byStatus = await db.StudentCoachPlans
            .GroupBy(p => p.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var byProvider = await db.StudentCoachPlans
            .Where(p => p.ProviderUsed != null)
            .GroupBy(p => p.ProviderUsed!)
            .Select(g => new { Provider = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        var failedErrors = await db.StudentCoachPlans
            .Where(p => p.Status == CoachStatus.Failed && p.LastError != null)
            .Select(p => p.LastError!)
            .ToListAsync(ct);

        var ruleCounts = new Dictionary<string, int>();
        const string prefix = "Validation failed: ";
        foreach (var error in failedErrors)
        {
            if (!error.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            var rules = error[prefix.Length..].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            foreach (var rule in rules)
            {
                ruleCounts[rule] = ruleCounts.GetValueOrDefault(rule) + 1;
            }
        }

        var body = new CoachStatsBody
        {
            ByStatus = byStatus.ToDictionary(x => x.Status.ToString(), x => x.Count),
            ByProvider = byProvider.ToDictionary(x => x.Provider, x => x.Count),
            TopValidationFailures = ruleCounts
                .OrderByDescending(kv => kv.Value)
                .Select(kv => new CoachValidationFailureCount { Rule = kv.Key, Count = kv.Value })
                .ToList(),
        };

        return Results.Ok(body);
    }

    private static CoachPlanReadyBody ToReadyBody(StudentCoachPlan plan)
    {
        var weeklyPlan = string.IsNullOrEmpty(plan.WeeklyPlanJson)
            ? []
            : JsonSerializer.Deserialize<List<CoachWeekPlanBody>>(plan.WeeklyPlanJson) ?? [];

        var skills = string.IsNullOrEmpty(plan.ProjectSkillsJson)
            ? []
            : JsonSerializer.Deserialize<List<string>>(plan.ProjectSkillsJson) ?? [];

        return new CoachPlanReadyBody
        {
            Headline = plan.Headline ?? string.Empty,
            Assessment = plan.Assessment ?? string.Empty,
            WeeklyPlan = weeklyPlan,
            ProjectSuggestion = new CoachProjectSuggestionBody
            {
                Title = plan.ProjectTitle,
                Description = plan.ProjectDesc,
                SkillsPractised = skills,
            },
            InterviewLine = plan.InterviewLine ?? string.Empty,
        };
    }
}
