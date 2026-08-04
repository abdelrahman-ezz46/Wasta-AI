using System.Security.Claims;
using Microsoft.Extensions.Logging;
using CoachDomain = Wasta.CareerCoach.Domain;
using ChatDomain = Wasta.SupportChat.Domain;

namespace Wasta.DevHost.Adapters;

/// <summary>
/// Reads the student id from the "student_id" claim that DevAuthHandler
/// puts there. Both modules declare their own tiny accessor interface (they
/// don't depend on each other), so one class implements both.
/// </summary>
public class DevCurrentStudentAccessor : CoachDomain.ICurrentStudentAccessor, ChatDomain.ICurrentStudentAccessor
{
    public const string StudentIdClaim = "student_id";

    public int? GetStudentId(ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(StudentIdClaim);
        return int.TryParse(raw, out var id) ? id : null;
    }
}

/// <summary>Writes audit entries to the log. The real app would persist these
/// to its AuditLog table.</summary>
public class ConsoleAuditLogWriter : CoachDomain.IAuditLogWriter
{
    private readonly ILogger<ConsoleAuditLogWriter> _logger;

    public ConsoleAuditLogWriter(ILogger<ConsoleAuditLogWriter> logger) => _logger = logger;

    public Task WriteAsync(string action, string? actorId, string details, CancellationToken ct)
    {
        _logger.LogInformation("AUDIT action={Action} actor={Actor} details={Details}", action, actorId ?? "(none)", details);
        return Task.CompletedTask;
    }
}
