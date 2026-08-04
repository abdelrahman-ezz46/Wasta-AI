namespace Wasta.CareerCoach.Domain;

// This module is self-contained: it does not assume the shape of your real
// StudentProfiles / AssessmentAttempts / Scores tables. Instead it depends on
// this narrow port, which your host app implements once against its actual
// entities (see IAssessmentDataProvider). Wire your implementation into DI
// alongside AddCareerCoach(...). Nothing else in this module touches your
// scoring schema.

public sealed record SectionScoreData(string Name, int Percent);

public sealed record AttemptScoreData(
    int StudentId,
    int AttemptId,
    int ScoreId,
    string Track,
    IReadOnlyList<SectionScoreData> Sections);

public sealed record StudentContextData(
    IReadOnlyList<string> Skills,
    IReadOnlyList<string> ProjectTitles,
    int? GraduationYear);

/// <summary>
/// Bridge into the host application's own scoring and profile data.
/// Implement this against your real entities; this module never reads or
/// writes the Wasta Score itself.
/// </summary>
public interface IAssessmentDataProvider
{
    Task<AttemptScoreData?> GetAttemptScoreAsync(int attemptId, CancellationToken ct);

    Task<StudentContextData> GetStudentContextAsync(int studentId, CancellationToken ct);

    /// <summary>The attempt behind the results page the student is currently viewing
    /// (typically their most recent scored attempt). Used by GET /coach-plan, which
    /// takes no attemptId - the host app decides what "current" means.</summary>
    Task<int?> GetCurrentAttemptIdAsync(int studentId, CancellationToken ct);
}

/// <summary>
/// Resolves the caller's student id from the current request. Implement this
/// against your real auth (JWT claim, session, etc.) and register it in DI;
/// this module never parses credentials itself.
/// </summary>
public interface ICurrentStudentAccessor
{
    int? GetStudentId(System.Security.Claims.ClaimsPrincipal user);
}
