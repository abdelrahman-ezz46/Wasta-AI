namespace Wasta.CareerCoach;

public class CoachOptions
{
    public const string SectionName = "CareerCoach";

    public string PromptPath { get; set; } = "Prompts/CareerCoach.v1.txt";

    /// <summary>Optional model override. Generation runs once per assessment and
    /// has to produce JSON matching a strict schema - a response that misses
    /// is rejected by the validator and retried, so it is worth spending a
    /// more capable model here. Null uses the provider's configured default.</summary>
    public string? Model { get; set; }
}
