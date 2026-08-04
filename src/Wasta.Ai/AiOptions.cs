namespace Wasta.Ai;

public class AiOptions
{
    public const string SectionName = "Ai";

    public bool Enabled { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxTokens { get; set; } = 1600;
    public double Temperature { get; set; } = 0.4;

    /// <summary>Provider names in fallthrough order, e.g. ["groq", "gemini"].</summary>
    public List<string> Chain { get; set; } = new();

    public Dictionary<string, AiProviderOptions> Providers { get; set; } = new();
}

public class AiProviderOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
}
