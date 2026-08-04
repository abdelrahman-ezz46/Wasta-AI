using System.Text.Json.Serialization;

namespace Wasta.CareerCoach.Services;

// Mirrors Part 2 "User message" in the spec exactly. Nothing beyond track,
// section scores, and a capped slice of skills/projects/grad year is ever
// sent - no name, email, university, city, or CV.
internal sealed class CoachRequestDto
{
    [JsonPropertyName("request_id")]
    public string RequestId { get; set; } = string.Empty;

    [JsonPropertyName("track")]
    public string Track { get; set; } = string.Empty;

    [JsonPropertyName("sections")]
    public List<CoachRequestSection> Sections { get; set; } = new();

    [JsonPropertyName("student_context")]
    public CoachRequestStudentContext StudentContext { get; set; } = new();
}

internal sealed class CoachRequestSection
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("percent")]
    public int Percent { get; set; }
}

internal sealed class CoachRequestStudentContext
{
    [JsonPropertyName("skills")]
    public List<string> Skills { get; set; } = new();

    [JsonPropertyName("project_titles")]
    public List<string> ProjectTitles { get; set; } = new();

    [JsonPropertyName("graduation_year")]
    public int? GraduationYear { get; set; }
}
