using System.Threading.Channels;

namespace Wasta.CareerCoach.BackgroundJobs;

/// <summary>
/// Bounded handoff from the submit request thread to the background worker.
/// Capacity 500: a launch-scale burst of submissions fits comfortably. If
/// the queue is ever full, TryEnqueue returns false and the caller does
/// nothing further - the row is already Pending in the database, and
/// CoachSweeperService will pick it up on its next pass.
/// </summary>
public class CoachGenerationQueue
{
    private const int Capacity = 500;

    // FullMode.Wait, not DropWrite: TryWrite never blocks under either mode,
    // but DropWrite returns true while silently discarding the item, so a
    // full queue would look like a successful enqueue. Wait makes TryWrite
    // return false when full, which is what lets the caller (and the
    // sweeper) know the job still needs picking up.
    private readonly Channel<int> _channel = Channel.CreateBounded<int>(new BoundedChannelOptions(Capacity)
    {
        SingleReader = true,
        SingleWriter = false,
        FullMode = BoundedChannelFullMode.Wait,
    });

    public bool TryEnqueue(int attemptId) => _channel.Writer.TryWrite(attemptId);

    public IAsyncEnumerable<int> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
