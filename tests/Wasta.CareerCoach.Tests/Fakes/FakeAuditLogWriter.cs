using Wasta.CareerCoach.Domain;

namespace Wasta.CareerCoach.Tests.Fakes;

public class FakeAuditLogWriter : IAuditLogWriter
{
    public List<(string Action, string? ActorId, string Details)> Entries { get; } = [];

    public Task WriteAsync(string action, string? actorId, string details, CancellationToken ct)
    {
        Entries.Add((action, actorId, details));
        return Task.CompletedTask;
    }
}
