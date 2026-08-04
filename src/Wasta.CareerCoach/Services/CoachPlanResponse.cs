using System.Text.Json.Serialization;

namespace Wasta.CareerCoach.Services;

/// <summary>Raw shape of the model's JSON output, per Prompts/CareerCoach.v1.txt.</summary>
public sealed class CoachPlanResponse
{
    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("version")]
    public string? Version { get; set; }

    [JsonPropertyName("data")]
    public CoachPlanData? Data { get; set; }

    [JsonPropertyName("warnings")]
    public List<string>? Warnings { get; set; }
}

public sealed class CoachPlanData
{
    [JsonPropertyName("headline")]
    public string? Headline { get; set; }

    [JsonPropertyName("assessment")]
    public string? Assessment { get; set; }

    [JsonPropertyName("weekly_plan")]
    public List<CoachWeekPlan>? WeeklyPlan { get; set; }

    [JsonPropertyName("project_suggestion")]
    public CoachProjectSuggestion? ProjectSuggestion { get; set; }

    [JsonPropertyName("interview_line")]
    public string? InterviewLine { get; set; }
}

public sealed class CoachWeekPlan
{
    [JsonPropertyName("week")]
    public int Week { get; set; }

    [JsonPropertyName("focus")]
    public string? Focus { get; set; }

    [JsonPropertyName("actions")]
    public List<string>? Actions { get; set; }

    [JsonPropertyName("checkpoint")]
    public string? Checkpoint { get; set; }
}

public sealed class CoachProjectSuggestion
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("skills_practised")]
    public List<string>? SkillsPractised { get; set; }
}
