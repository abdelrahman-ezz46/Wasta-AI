using Microsoft.EntityFrameworkCore;
using Wasta.CareerCoach.BackgroundJobs;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Domain;

namespace Wasta.CareerCoach.Services;

/// <summary>
/// Call this from your existing submit flow (e.g. AssessmentService), right
/// after scoring completes and before returning the HTTP response. Do NOT
/// await CoachGenerationService from that path - this only inserts a
/// Pending row and hands the attempt id to the background queue, both of
/// which are fast, non-blocking operations.
/// </summary>
public class CoachPlanTrigger
{
    private readonly CoachDbContext _db;
    private readonly CoachGenerationQueue _queue;

    public CoachPlanTrigger(CoachDbContext db, CoachGenerationQueue queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task EnqueueGenerationAsync(int studentId, int attemptId, int scoreId, CancellationToken ct)
    {
        var existing = await _db.StudentCoachPlans.FirstOrDefaultAsync(p => p.AttemptId == attemptId, ct);
        if (existing is null)
        {
            _db.StudentCoachPlans.Add(new StudentCoachPlan
            {
                StudentId = studentId,
                AttemptId = attemptId,
                ScoreId = scoreId,
                Status = CoachStatus.Pending,
            });

            await _db.SaveChangesAsync(ct);
        }

        // Queue-full is not an error: the row is already Pending and
        // CoachSweeperService will pick it up on its next pass.
        _queue.TryEnqueue(attemptId);
    }
}
