using System.Text.Json.Serialization;

namespace Wasta.CareerCoach.Api;

public sealed class CoachPlanPendingBody
{
    [JsonPropertyName("status")]
    public string Status => "pending";
}

public sealed class CoachPlanUnavailableBody
{
    [JsonPropertyName("status")]
    public string Status => "unavailable";
}

public sealed class CoachPlanReadyBody
{
    [JsonPropertyName("status")]
    public string Status => "ready";

    [JsonPropertyName("headline")]
    public required string Headline { get; init; }

    [JsonPropertyName("assessment")]
    public required string Assessment { get; init; }

    [JsonPropertyName("weekly_plan")]
    public required List<CoachWeekPlanBody> WeeklyPlan { get; init; }

    [JsonPropertyName("project_suggestion")]
    public required CoachProjectSuggestionBody ProjectSuggestion { get; init; }

    [JsonPropertyName("interview_line")]
    public required string InterviewLine { get; init; }
}

public sealed class CoachWeekPlanBody
{
    [JsonPropertyName("week")]
    public int Week { get; init; }

    [JsonPropertyName("focus")]
    public string? Focus { get; init; }

    [JsonPropertyName("actions")]
    public List<string> Actions { get; init; } = [];

    [JsonPropertyName("checkpoint")]
    public string? Checkpoint { get; init; }
}

public sealed class CoachProjectSuggestionBody
{
    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("skills_practised")]
    public List<string> SkillsPractised { get; init; } = [];
}

public sealed class CoachStatsBody
{
    [JsonPropertyName("by_status")]
    public required Dictionary<string, int> ByStatus { get; init; }

    [JsonPropertyName("by_provider")]
    public required Dictionary<string, int> ByProvider { get; init; }

    [JsonPropertyName("top_validation_failures")]
    public required List<CoachValidationFailureCount> TopValidationFailures { get; init; }
}

public sealed class CoachValidationFailureCount
{
    [JsonPropertyName("rule")]
    public required string Rule { get; init; }

    [JsonPropertyName("count")]
    public required int Count { get; init; }
}
