using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wasta.CareerCoach.Services;

namespace Wasta.CareerCoach.BackgroundJobs;

/// <summary>
/// Single consumer of <see cref="CoachGenerationQueue"/>. A fixed delay
/// between jobs keeps a burst of submissions from tripping a free-tier AI
/// rate limit; this is a deliberate throughput cap, not a bug.
/// </summary>
public class CoachGenerationWorker : BackgroundService
{
    private static readonly TimeSpan DelayBetweenJobs = TimeSpan.FromSeconds(2);

    private readonly CoachGenerationQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CoachGenerationWorker> _logger;

    public CoachGenerationWorker(CoachGenerationQueue queue, IServiceScopeFactory scopeFactory, ILogger<CoachGenerationWorker> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var attemptId in _queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var generationService = scope.ServiceProvider.GetRequiredService<CoachGenerationService>();
                await generationService.GenerateAsync(attemptId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Coach generation worker failed on attempt {AttemptId}", attemptId);
            }

            try
            {
                await Task.Delay(DelayBetweenJobs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
