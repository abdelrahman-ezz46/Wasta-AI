using Wasta.CareerCoach.Domain;

namespace Wasta.CareerCoach.Tests.Fakes;

public class FakeAssessmentDataProvider : IAssessmentDataProvider
{
    private readonly Dictionary<int, AttemptScoreData> _attempts = new();
    private readonly Dictionary<int, StudentContextData> _contexts = new();
    private readonly Dictionary<int, int> _currentAttemptByStudent = new();

    public FakeAssessmentDataProvider WithAttempt(AttemptScoreData attempt)
    {
        _attempts[attempt.AttemptId] = attempt;
        _currentAttemptByStudent[attempt.StudentId] = attempt.AttemptId;
        return this;
    }

    public FakeAssessmentDataProvider WithContext(int studentId, StudentContextData context)
    {
        _contexts[studentId] = context;
        return this;
    }

    public Task<AttemptScoreData?> GetAttemptScoreAsync(int attemptId, CancellationToken ct)
        => Task.FromResult(_attempts.GetValueOrDefault(attemptId));

    public Task<StudentContextData> GetStudentContextAsync(int studentId, CancellationToken ct)
        => Task.FromResult(_contexts.GetValueOrDefault(studentId) ?? new StudentContextData([], [], null));

    public Task<int?> GetCurrentAttemptIdAsync(int studentId, CancellationToken ct)
        => Task.FromResult(_currentAttemptByStudent.TryGetValue(studentId, out var id) ? id : (int?)null);
}
