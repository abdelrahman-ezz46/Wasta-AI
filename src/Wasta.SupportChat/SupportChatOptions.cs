namespace Wasta.SupportChat;

public class SupportChatOptions
{
    public const string SectionName = "SupportChat";

    public string PromptPath { get; set; } = "Prompts/SupportChat.v1.txt";
    public string KnowledgePath { get; set; } = "Knowledge/PlatformKnowledge.v1.md";

    /// <summary>Chat answers should be short and factual - smaller and colder than
    /// the Career Coach's generation defaults (which write a long structured plan).</summary>
    public int MaxTokens { get; set; } = 500;
    public double Temperature { get; set; } = 0.3;

    /// <summary>How many prior turns (user + assistant messages combined) are sent
    /// as context. Keeps token usage and latency bounded on long conversations.</summary>
    public int MaxHistoryTurns { get; set; } = 12;

    /// <summary>When a logged-in student opens a brand-new session, how many of
    /// their most recent messages from EARLIER sessions get seeded in as
    /// starting context. Anonymous (visitor-only) sessions never get this -
    /// a browser-local visitor id isn't a secure enough identity to carry
    /// memory across visits.</summary>
    public int CrossSessionMemoryTurns { get; set; } = 12;

    /// <summary>How many open job listings to fetch and offer the model per
    /// turn. Kept small - these are injected as extra prompt content on
    /// every message, so cost scales directly with this number.</summary>
    public int MaxJobListings { get; set; } = 5;

    /// <summary>Hard cap per session. Once hit, further messages get a static
    /// "contact support" reply instead of calling the AI - keeps a single
    /// runaway conversation from burning the AI budget.</summary>
    public int MaxMessagesPerSession { get; set; } = 40;

    /// <summary>Minimum time between two messages in the same session. A basic
    /// throttle against rapid-fire scripted abuse; not a substitute for
    /// real infra-level rate limiting if this ever needs to scale.</summary>
    public double MinSecondsBetweenMessages { get; set; } = 2;

    public int MaxMessageLength { get; set; } = 2000;
}
