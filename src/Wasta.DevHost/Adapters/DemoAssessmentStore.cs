using System.Collections.Concurrent;
using Wasta.CareerCoach.Domain;

namespace Wasta.DevHost.Adapters;

/// <summary>
/// Stands in for the real assessment/scoring tables. Everything here is
/// fake demo data held in memory - in the real app this adapter would read
/// StudentProfiles, AssessmentAttempts, and Scores instead. The important
/// part is the shape of what it returns, which is exactly what the Career
/// Coach consumes.
/// </summary>
public class DemoAssessmentStore : IAssessmentDataProvider
{
    private readonly ConcurrentDictionary<int, AttemptScoreData> _attempts = new();
    private readonly ConcurrentDictionary<int, StudentContextData> _contexts = new();
    private readonly ConcurrentDictionary<int, int> _currentAttemptByStudent = new();
    private int _nextAttemptId = 1000;
    private int _nextScoreId = 5000;

    public DemoAssessmentStore()
    {
        // Two seeded students so cross-student isolation can be exercised
        // by hand, not just in tests.
        _contexts[1] = new StudentContextData(
            Skills: ["Python", "pandas", "scikit-learn"],
            ProjectTitles: ["Movie recommender", "Titanic survival model"],
            GraduationYear: 2028);

        _contexts[2] = new StudentContextData(
            Skills: ["JavaScript", "React"],
            ProjectTitles: ["Portfolio site"],
            GraduationYear: 2027);
    }

    /// <summary>Overrides a student's profile context. Exists so the guardrail
    /// script can plant a prompt-injection string in the skills list and see
    /// what a real model does with it.</summary>
    public void SetStudentContext(int studentId, StudentContextData context) => _contexts[studentId] = context;

    /// <summary>Simulates the host app's submit flow producing a fresh scored
    /// attempt. Returns the ids the Career Coach needs to generate against.</summary>
    public AttemptScoreData RecordAttempt(int studentId, IReadOnlyList<SectionScoreData> sections)
    {
        var attemptId = Interlocked.Increment(ref _nextAttemptId);
        var scoreId = Interlocked.Increment(ref _nextScoreId);

        var attempt = new AttemptScoreData(studentId, attemptId, scoreId, "Data & AI", sections);
        _attempts[attemptId] = attempt;
        _currentAttemptByStudent[studentId] = attemptId;
        return attempt;
    }

    public Task<AttemptScoreData?> GetAttemptScoreAsync(int attemptId, CancellationToken ct)
        => Task.FromResult(_attempts.GetValueOrDefault(attemptId));

    public Task<StudentContextData> GetStudentContextAsync(int studentId, CancellationToken ct)
        => Task.FromResult(_contexts.GetValueOrDefault(studentId) ?? new StudentContextData([], [], null));

    public Task<int?> GetCurrentAttemptIdAsync(int studentId, CancellationToken ct)
        => Task.FromResult(_currentAttemptByStudent.TryGetValue(studentId, out var id) ? id : (int?)null);
}
