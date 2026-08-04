namespace Wasta.CareerCoach.Domain;

/// <summary>
/// One AI-generated study plan per assessment attempt. Generated once in the
/// background at submit time; the results page reads this row and never
/// triggers generation itself. See Prompts/CareerCoach.v1.txt for the schema
/// that WeeklyPlan / ProjectSkills are validated against before being stored.
/// </summary>
public class StudentCoachPlan
{
    public int Id { get; set; }

    public int StudentId { get; set; }
    public int AttemptId { get; set; }
    public int ScoreId { get; set; }

    public CoachStatus Status { get; set; } = CoachStatus.Pending;

    public string? Headline { get; set; }
    public string? Assessment { get; set; }

    /// <summary>Serialized array of 4 WeekPlan objects, weeks 1-4.</summary>
    public string? WeeklyPlanJson { get; set; }

    public string? ProjectTitle { get; set; }
    public string? ProjectDesc { get; set; }

    /// <summary>Serialized string array.</summary>
    public string? ProjectSkillsJson { get; set; }

    public string? InterviewLine { get; set; }

    public string PromptVersion { get; set; } = "1.0";
    public string? ProviderUsed { get; set; }

    public int AttemptCount { get; set; }
    public string? LastError { get; set; }

    public DateTimeOffset? GeneratedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
