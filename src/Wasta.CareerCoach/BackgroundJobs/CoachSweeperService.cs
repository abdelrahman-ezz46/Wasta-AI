using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Domain;
using Wasta.CareerCoach.Services;

namespace Wasta.CareerCoach.BackgroundJobs;

/// <summary>
/// Safety net for jobs that never made it through the queue (a full queue,
/// a process restart mid-generation) or that failed transiently. Retries
/// Failed plans with fewer than 3 attempts, oldest first, capped at 10 per
/// pass so a bad prompt version can't burn through the whole failed pool at
/// once.
/// </summary>
public class CoachSweeperService : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromMinutes(10);
    private const int MaxAttemptCount = 3;
    private const int MaxPerPass = 10;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CoachSweeperService> _logger;

    public CoachSweeperService(IServiceScopeFactory scopeFactory, ILogger<CoachSweeperService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(SweepInterval);

        do
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Coach plan sweep failed");
            }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachDbContext>();
        var generationService = scope.ServiceProvider.GetRequiredService<CoachGenerationService>();

        var candidates = await db.StudentCoachPlans
            .Where(p => p.Status == CoachStatus.Failed && p.AttemptCount < MaxAttemptCount)
            .OrderBy(p => p.CreatedAt)
            .Take(MaxPerPass)
            .Select(p => p.AttemptId)
            .ToListAsync(ct);

        foreach (var attemptId in candidates)
        {
            await generationService.GenerateAsync(attemptId, ct);
        }
    }
}
