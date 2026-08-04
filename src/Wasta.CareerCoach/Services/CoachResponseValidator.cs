using System.Text.Json;
using System.Text.RegularExpressions;

namespace Wasta.CareerCoach.Services;

public sealed record CoachValidationResult(bool IsValid, CoachPlanResponse? Response, IReadOnlyList<string> FailedRules)
{
    public static CoachValidationResult Valid(CoachPlanResponse response) => new(true, response, []);

    public static CoachValidationResult Invalid(params string[] rules) => new(false, null, rules);
}

/// <summary>
/// Enforces the response contract from Prompts/CareerCoach.v1.txt section
/// "OUTPUT SCHEMA" plus the leak-prevention checks. Any failure means the
/// response is rejected and the caller should retry once, then give up.
/// </summary>
public static partial class CoachResponseValidator
{
    [GeneratedRegex(@"```(?:json)?\s*|\s*```", RegexOptions.IgnoreCase)]
    private static partial Regex FenceRegex();

    /// <summary>
    /// Rule 4 (never mention the numeric score/percentage/percentile) covers
    /// more than the "%" symbol. A model that writes "you scored 41 percent"
    /// or "41 out of 100" has leaked the score just as surely, so all the
    /// spellings are rejected, not only the punctuation.
    /// </summary>
    /// The "\D{0,15}" gaps are deliberately narrow and digit-free: they catch
    /// "you scored 41" and "your score of 41" while leaving legitimate study
    /// advice like "score each model and compare 3 runs" alone.
    [GeneratedRegex(
        @"\d+(\.\d+)?\s*%|percentile|per\s?cent(s|age)?\b|\b\d+\s*out\s+of\s+\d+|\b\d+\s*/\s*\d{2,}|\bscor(e|ed|es)\b\D{0,15}\d",
        RegexOptions.IgnoreCase)]
    private static partial Regex ScoreLeakRegex();

    /// <summary>
    /// Rule 2 (never touch employment prospects). Word-boundary anchored:
    /// a plain substring match rejects legitimate text - "Hampshire" and
    /// "Yorkshire" both contain "hire" - which silently burns the single
    /// retry and fails an otherwise good plan.
    /// </summary>
    [GeneratedRegex(
        @"\b(hire[sd]?|hiring|salary|salaries|job offers?)\b|\byou will get\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex ProhibitedEmploymentRegex();

    public static CoachValidationResult Validate(string rawResponse)
    {
        var stripped = FenceRegex().Replace(rawResponse, string.Empty).Trim();

        CoachPlanResponse? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<CoachPlanResponse>(stripped, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (JsonException)
        {
            return CoachValidationResult.Invalid("invalid_json");
        }

        if (parsed is null)
        {
            return CoachValidationResult.Invalid("invalid_json");
        }

        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(parsed.Status) || string.Equals(parsed.Status, "error", StringComparison.OrdinalIgnoreCase))
        {
            failures.Add("status_missing_or_error");
        }

        var data = parsed.Data;
        if (data is null)
        {
            failures.Add("data_missing");
        }
        else
        {
            if (string.IsNullOrWhiteSpace(data.Headline) || data.Headline.Length < 20 || data.Headline.Length > 80)
            {
                failures.Add("headline_length");
            }

            if (string.IsNullOrWhiteSpace(data.Assessment) || data.Assessment.Length < 250 || data.Assessment.Length > 700)
            {
                failures.Add("assessment_length");
            }

            ValidateWeeklyPlan(data.WeeklyPlan, failures);

            if (data.ProjectSuggestion is null
                || string.IsNullOrWhiteSpace(data.ProjectSuggestion.Title)
                || string.IsNullOrWhiteSpace(data.ProjectSuggestion.Description))
            {
                failures.Add("project_suggestion_incomplete");
            }

            if (string.IsNullOrWhiteSpace(data.InterviewLine) || data.InterviewLine.Length > 300)
            {
                failures.Add("interview_line_invalid");
            }
        }

        if (ScoreLeakRegex().IsMatch(stripped))
        {
            failures.Add("leaked_score_reference");
        }

        if (ProhibitedEmploymentRegex().IsMatch(stripped))
        {
            failures.Add("prohibited_hiring_language");
        }

        return failures.Count == 0
            ? CoachValidationResult.Valid(parsed)
            : new CoachValidationResult(false, parsed, failures);
    }

    private static void ValidateWeeklyPlan(List<CoachWeekPlan>? weeklyPlan, List<string> failures)
    {
        if (weeklyPlan is null || weeklyPlan.Count != 4)
        {
            failures.Add("weekly_plan_count");
            return;
        }

        for (var i = 0; i < weeklyPlan.Count; i++)
        {
            var week = weeklyPlan[i];
            if (week.Week != i + 1)
            {
                failures.Add("weekly_plan_order");
            }

            if (week.Actions is null || week.Actions.Count is < 2 or > 3)
            {
                failures.Add("weekly_plan_actions_count");
            }

            if (string.IsNullOrWhiteSpace(week.Focus) || string.IsNullOrWhiteSpace(week.Checkpoint))
            {
                failures.Add("weekly_plan_missing_fields");
            }
        }
    }
}
