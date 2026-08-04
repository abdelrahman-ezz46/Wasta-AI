using Wasta.CareerCoach.Services;
using Wasta.CareerCoach.Tests.Fakes;

namespace Wasta.CareerCoach.Tests;

public class CoachResponseValidatorTests
{
    [Fact]
    public void Validate_WellFormedResponse_IsValid()
    {
        var result = CoachResponseValidator.Validate(SampleResponses.ValidJson());

        Assert.True(result.IsValid);
        Assert.Empty(result.FailedRules);
    }

    [Fact]
    public void Validate_ResponseWrappedInMarkdownFences_StillParses()
    {
        var fenced = "```json\n" + SampleResponses.ValidJson() + "\n```";

        var result = CoachResponseValidator.Validate(fenced);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validate_ResponseContainingPercentage_IsRejected()
    {
        var withPercentage = SampleResponses.ValidJson(
            assessment: SampleResponses.Assessment + " You scored 41% in this section overall today.");

        var result = CoachResponseValidator.Validate(withPercentage);

        Assert.False(result.IsValid);
        Assert.Contains("leaked_score_reference", result.FailedRules);
    }

    [Fact]
    public void Validate_ResponseContainingPercentileWord_IsRejected()
    {
        var withPercentile = SampleResponses.ValidJson(
            assessment: SampleResponses.Assessment + " This places you in a percentile band overall.");

        var result = CoachResponseValidator.Validate(withPercentile);

        Assert.False(result.IsValid);
        Assert.Contains("leaked_score_reference", result.FailedRules);
    }

    [Theory]
    [InlineData("This will help you get hired faster than other candidates today.")]
    [InlineData("Companies are more likely to offer you a job offer after this plan overall.")]
    [InlineData("Expect a higher salary once you finish this four week plan overall.")]
    public void Validate_ResponseContainingHiringLanguage_IsRejected(string extra)
    {
        var withHiringLanguage = SampleResponses.ValidJson(assessment: SampleResponses.Assessment + " " + extra);

        var result = CoachResponseValidator.Validate(withHiringLanguage);

        Assert.False(result.IsValid);
        Assert.Contains("prohibited_hiring_language", result.FailedRules);
    }

    [Fact]
    public void Validate_MalformedJson_IsRejected()
    {
        var result = CoachResponseValidator.Validate("this is not { json");

        Assert.False(result.IsValid);
        Assert.Contains("invalid_json", result.FailedRules);
    }

    [Fact]
    public void Validate_ErrorStatus_IsRejected()
    {
        var result = CoachResponseValidator.Validate("""{"status":"error","version":"1.0","data":null,"warnings":[]}""");

        Assert.False(result.IsValid);
        Assert.Contains("status_missing_or_error", result.FailedRules);
    }

    [Fact]
    public void Validate_WeeklyPlanWithThreeEntries_IsRejected()
    {
        var json = SampleResponses.ValidJson();
        // Corrupt by removing the trailing week from the array (simulate a short plan).
        var trimmed = json.Replace(
            """,{"week":4,"focus":"Pipelines outside notebooks","actions":["Move one script into a reusable function","Add a small test for that function"],"checkpoint":"The script runs without manual notebook steps."}""",
            string.Empty);

        var result = CoachResponseValidator.Validate(trimmed);

        Assert.False(result.IsValid);
        Assert.Contains("weekly_plan_count", result.FailedRules);
    }
}
