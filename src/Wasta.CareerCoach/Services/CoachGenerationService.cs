using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Wasta.Ai;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Domain;

namespace Wasta.CareerCoach.Services;

/// <summary>
/// Generates (or re-generates) one AI study plan for an assessment attempt.
/// Never called on a request a user is waiting on - see
/// BackgroundJobs/CoachGenerationWorker and CoachSweeperService for the only
/// two callers. Never throws: every failure path is caught, logged, and
/// persisted as Status = Failed so the caller can move on to the next job.
/// </summary>
public class CoachGenerationService
{
    private const int MaxAttemptsPerCall = 2;
    private const int MaxSkills = 12;
    private const int MaxProjectTitles = 6;
    private const int MaxStoredErrorLength = 500;

    private readonly CoachDbContext _db;
    private readonly IAssessmentDataProvider _dataProvider;
    private readonly AiProviderChain _providerChain;
    private readonly AiOptions _aiOptions;
    private readonly CoachOptions _coachOptions;
    private readonly ILogger<CoachGenerationService> _logger;

    public CoachGenerationService(
        CoachDbContext db,
        IAssessmentDataProvider dataProvider,
        AiProviderChain providerChain,
        IOptions<AiOptions> aiOptions,
        IOptions<CoachOptions> coachOptions,
        ILogger<CoachGenerationService> logger)
    {
        _db = db;
        _dataProvider = dataProvider;
        _providerChain = providerChain;
        _aiOptions = aiOptions.Value;
        _coachOptions = coachOptions.Value;
        _logger = logger;
    }

    public async Task<CoachResult> GenerateAsync(int attemptId, CancellationToken ct)
    {
        try
        {
            return await GenerateInternalAsync(attemptId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error generating coach plan for attempt {AttemptId}", attemptId);
            var plan = await MarkFailedAsync(attemptId, null, null, null, ex.Message, ct);
            return new CoachResult(false, plan, ex.Message);
        }
    }

    private async Task<CoachResult> GenerateInternalAsync(int attemptId, CancellationToken ct)
    {
        var existing = await _db.StudentCoachPlans.FirstOrDefaultAsync(p => p.AttemptId == attemptId, ct);
        if (existing is { Status: CoachStatus.Ready })
        {
            return new CoachResult(true, existing, null);
        }

        if (!_aiOptions.Enabled)
        {
            var skipped = await UpsertSkippedAsync(existing, attemptId, ct);
            return new CoachResult(false, skipped, "AI career coach is disabled.");
        }

        var attemptData = await _dataProvider.GetAttemptScoreAsync(attemptId, ct);
        if (attemptData is null)
        {
            var plan = await MarkFailedAsync(attemptId, existing, null, null, "Attempt not found.", ct);
            return new CoachResult(false, plan, "Attempt not found.");
        }

        var studentContext = await _dataProvider.GetStudentContextAsync(attemptData.StudentId, ct);

        string systemPrompt;
        try
        {
            systemPrompt = await PromptFile.ReadAllTextAsync(_coachOptions.PromptPath, ct);
        }
        catch (IOException ex)
        {
            // Logged loudly: a misconfigured prompt path fails every single
            // generation, and without a log line it looks identical to the
            // provider being down.
            _logger.LogError(ex, "Coach prompt file could not be read from configured path {PromptPath}", _coachOptions.PromptPath);
            var plan = await MarkFailedAsync(attemptId, existing, attemptData.StudentId, attemptData.ScoreId, $"Prompt file unreadable: {ex.Message}", ct);
            return new CoachResult(false, plan, ex.Message);
        }

        var userJson = BuildUserMessage(attemptId, attemptData, studentContext);

        CoachValidationResult? validation = null;
        AiCompletionResult? completion = null;
        string? lastError = null;

        for (var attempt = 1; attempt <= MaxAttemptsPerCall; attempt++)
        {
            try
            {
                completion = await _providerChain.CompleteWithMetadataAsync(
                    systemPrompt,
                    [new AiChatTurn("user", userJson)],
                    new AiCallOptions(Model: _coachOptions.Model),
                    ct);
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning(ex, "Provider chain failed for attempt {AttemptId} on try {Try}", attemptId, attempt);
                break;
            }

            validation = CoachResponseValidator.Validate(completion.Content);
            if (validation.IsValid)
            {
                break;
            }

            _logger.LogWarning(
                "Coach response for attempt {AttemptId} failed validation on try {Try}: {Rules}. Raw response: {Raw}",
                attemptId, attempt, string.Join(",", validation.FailedRules), completion.Content);
            lastError = $"Validation failed: {string.Join(",", validation.FailedRules)}";
        }

        if (validation is not { IsValid: true } || completion is null)
        {
            var plan = await MarkFailedAsync(attemptId, existing, attemptData.StudentId, attemptData.ScoreId, lastError ?? "Unknown failure.", ct);
            return new CoachResult(false, plan, lastError);
        }

        var data = validation.Response!.Data!;

        var savedPlan = existing ?? new StudentCoachPlan { AttemptId = attemptId };
        savedPlan.StudentId = attemptData.StudentId;
        savedPlan.ScoreId = attemptData.ScoreId;
        savedPlan.Status = CoachStatus.Ready;
        savedPlan.Headline = data.Headline;
        savedPlan.Assessment = data.Assessment;
        savedPlan.WeeklyPlanJson = JsonSerializer.Serialize(data.WeeklyPlan);
        savedPlan.ProjectTitle = data.ProjectSuggestion!.Title;
        savedPlan.ProjectDesc = data.ProjectSuggestion.Description;
        savedPlan.ProjectSkillsJson = JsonSerializer.Serialize(data.ProjectSuggestion.SkillsPractised ?? []);
        savedPlan.InterviewLine = data.InterviewLine;
        savedPlan.PromptVersion = "1.0";
        savedPlan.ProviderUsed = completion.ProviderName;
        savedPlan.LastError = null;
        savedPlan.GeneratedAt = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            _db.StudentCoachPlans.Add(savedPlan);
        }

        await _db.SaveChangesAsync(ct);

        return new CoachResult(true, savedPlan, null);
    }

    private static string BuildUserMessage(int attemptId, AttemptScoreData attemptData, StudentContextData studentContext)
    {
        var dto = new CoachRequestDto
        {
            RequestId = $"coach-{attemptId}",
            Track = attemptData.Track,
            Sections = attemptData.Sections
                .Select(s => new CoachRequestSection { Name = s.Name, Percent = s.Percent })
                .ToList(),
            StudentContext = new CoachRequestStudentContext
            {
                Skills = studentContext.Skills.Take(MaxSkills).ToList(),
                ProjectTitles = studentContext.ProjectTitles.Take(MaxProjectTitles).ToList(),
                GraduationYear = studentContext.GraduationYear,
            },
        };

        return JsonSerializer.Serialize(dto);
    }

    private async Task<StudentCoachPlan> UpsertSkippedAsync(StudentCoachPlan? existing, int attemptId, CancellationToken ct)
    {
        var plan = existing ?? new StudentCoachPlan { AttemptId = attemptId };
        plan.Status = CoachStatus.Skipped;
        plan.LastError = "AI career coach disabled by configuration.";

        if (existing is null)
        {
            _db.StudentCoachPlans.Add(plan);
        }

        await _db.SaveChangesAsync(ct);
        return plan;
    }

    private async Task<StudentCoachPlan> MarkFailedAsync(
        int attemptId, StudentCoachPlan? existing, int? studentId, int? scoreId, string error, CancellationToken ct)
    {
        existing ??= await _db.StudentCoachPlans.FirstOrDefaultAsync(p => p.AttemptId == attemptId, ct);

        var plan = existing ?? new StudentCoachPlan { AttemptId = attemptId };
        if (studentId.HasValue)
        {
            plan.StudentId = studentId.Value;
        }

        if (scoreId.HasValue)
        {
            plan.ScoreId = scoreId.Value;
        }

        plan.Status = CoachStatus.Failed;
        plan.AttemptCount += 1;
        plan.LastError = error.Length > MaxStoredErrorLength ? error[..MaxStoredErrorLength] : error;

        if (existing is null)
        {
            _db.StudentCoachPlans.Add(plan);
        }

        await _db.SaveChangesAsync(ct);
        return plan;
    }
}
