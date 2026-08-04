namespace Wasta.CareerCoach.Domain;

/// <summary>
/// Bridge into the host app's own audit log. Implement against your real
/// AuditLog table/store and register it in DI.
/// </summary>
public interface IAuditLogWriter
{
    Task WriteAsync(string action, string? actorId, string details, CancellationToken ct);
}
