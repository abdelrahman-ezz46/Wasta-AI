using System.Text.Json;

namespace Wasta.CareerCoach.Tests.Fakes;

public static class SampleResponses
{
    public const string Headline = "Close the gap between statistics and applied modelling";

    public const string Assessment =
        "Your data handling is solid, but statistics and machine learning fundamentals are " +
        "noticeably behind it, and that gap is what will slow you down once a model needs " +
        "explaining rather than just running. Applied modelling sits in between: usable, but " +
        "shaky under questions about why a result holds. SQL is the weakest section, which " +
        "matters most once data lives in a database instead of a clean notebook file.";

    public const string InterviewLine =
        "I am strongest at building models quickly and I am now deliberately practising the " +
        "statistics that explain why they work.";

    /// <summary>A response that satisfies every rule in CoachResponseValidator.</summary>
    public static string ValidJson(string? headline = null, string? assessment = null, string? interviewLine = null)
    {
        var payload = new
        {
            status = "ok",
            version = "1.0",
            data = new
            {
                headline = headline ?? Headline,
                assessment = assessment ?? Assessment,
                weekly_plan = new object[]
                {
                    new
                    {
                        week = 1,
                        focus = "Statistics fundamentals",
                        actions = new[] { "Review probability distributions", "Work through hypothesis testing exercises" },
                        checkpoint = "You can explain a p-value without notes.",
                    },
                    new
                    {
                        week = 2,
                        focus = "SQL joins and aggregation",
                        actions = new[] { "Write JOIN queries across three tables", "Practise GROUP BY with HAVING clauses" },
                        checkpoint = "You can join and aggregate without looking up syntax.",
                    },
                    new
                    {
                        week = 3,
                        focus = "Model evaluation",
                        actions = new[] { "Compare models with cross-validation", "Read about bias and variance tradeoffs", "Rebuild one prior model with proper evaluation" },
                        checkpoint = "You can justify a model choice with a metric.",
                    },
                    new
                    {
                        week = 4,
                        focus = "Pipelines outside notebooks",
                        actions = new[] { "Move one script into a reusable function", "Add a small test for that function" },
                        checkpoint = "The script runs without manual notebook steps.",
                    },
                },
                project_suggestion = new
                {
                    title = "Churn model with a SQL backend",
                    description = "Build a small churn predictor that pulls its training data with SQL joins across " +
                                   "at least three tables instead of a flat CSV, then evaluate it with cross-validation " +
                                   "so the applied modelling and SQL gaps are both exercised in one project.",
                    skills_practised = new[] { "SQL", "cross-validation", "feature engineering" },
                },
                interview_line = interviewLine ?? InterviewLine,
            },
            warnings = Array.Empty<string>(),
        };

        return JsonSerializer.Serialize(payload);
    }
}
