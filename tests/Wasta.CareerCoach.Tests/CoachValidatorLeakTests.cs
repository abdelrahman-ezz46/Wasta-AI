using Wasta.CareerCoach.Services;
using Wasta.CareerCoach.Tests.Fakes;

namespace Wasta.CareerCoach.Tests;

/// <summary>
/// Regression tests for two validator defects found in QA:
///  - spelled-out scores ("41 percent", "41 out of 100") slipped past a
///    regex that only looked for the "%" symbol;
///  - substring matching on "hire" rejected legitimate text containing
///    "Hampshire"/"Yorkshire", burning the retry and failing a good plan.
/// Both directions matter: a false negative leaks the score onto the page,
/// a false positive throws away a valid plan.
/// </summary>
public class CoachValidatorLeakTests
{
    private static bool RejectedFor(string extraSentence, string expectedRule)
    {
        var json = SampleResponses.ValidJson(assessment: SampleResponses.Assessment + " " + extraSentence);
        var result = CoachResponseValidator.Validate(json);
        return !result.IsValid && result.FailedRules.Contains(expectedRule);
    }

    [Theory]
    [InlineData("You scored 41 percent on this section overall.")]
    [InlineData("That is 41 per cent of the available marks overall.")]
    [InlineData("Your score of 41 sits below the others overall here.")]
    [InlineData("You scored 41 out of 100 on this section overall.")]
    [InlineData("The result was 41/100 across the whole assessment.")]
    [InlineData("Your percentage was lower here than elsewhere overall.")]
    [InlineData("You landed in a lower percentile band this time.")]
    public void Validate_ScoreLeakInAnySpelling_IsRejected(string leak)
    {
        Assert.True(RejectedFor(leak, "leaked_score_reference"), $"Should have rejected: {leak}");
    }

    [Theory]
    [InlineData("This will help you get hired more quickly than others.")]
    [InlineData("Companies are hiring for this skill set at the moment.")]
    [InlineData("Expect a higher salary once you finish this plan overall.")]
    [InlineData("A job offer usually follows this kind of preparation.")]
    public void Validate_EmploymentClaims_AreRejected(string claim)
    {
        Assert.True(RejectedFor(claim, "prohibited_hiring_language"), $"Should have rejected: {claim}");
    }

    [Theory]
    [InlineData("Many teams in Hampshire use this exact toolchain daily.")]
    [InlineData("Yorkshire and Cheshire both run meetups on this topic.")]
    [InlineData("Focus on higher-order functions before moving onward.")]
    [InlineData("Score each model and then compare all three runs.")]
    [InlineData("Rebuild the notebook using three separate data sources.")]
    public void Validate_LegitimateText_IsNotFalselyRejected(string legitimate)
    {
        var json = SampleResponses.ValidJson(assessment: SampleResponses.Assessment + " " + legitimate);

        var result = CoachResponseValidator.Validate(json);

        Assert.True(result.IsValid, $"Should NOT have rejected: {legitimate} (rules: {string.Join(",", result.FailedRules)})");
    }
}
