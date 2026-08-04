using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Wasta.Ai;

namespace Wasta.SupportChat.Services;

public sealed record KnowledgeBase(string Text, int UnresolvedTodoCount);

/// <summary>
/// Loads the platform knowledge document that gets injected into the chat
/// system prompt.
///
/// Two things this does beyond reading the file:
///
/// 1. Strips unresolved "[TODO: ...]" markers. The knowledge base is a
///    living document that product owners edit in place, so half-written
///    drafts are normal and expected. But the whole file is pasted into the
///    system prompt, which means a model can quote those drafts straight
///    back to a student - internal notes ("[TODO: how does someone reset a
///    password]") surfacing as a customer-facing answer. Stripping them
///    leaves a genuine gap, which is the correct outcome: the prompt
///    already instructs the model to say it doesn't know and point to
///    support.
///
/// 2. Caches by last-write time. The prompt was previously re-read from
///    disk on every single chat message - two file reads per request on a
///    live endpoint. Keying the cache on the file's timestamp keeps the
///    edit-without-redeploy property while making the steady state a
///    dictionary lookup.
/// </summary>
public static partial class KnowledgeBaseLoader
{
    private static readonly ConcurrentDictionary<string, (DateTime Stamp, KnowledgeBase Value)> Cache = new();

    /// <summary>Matches "[TODO: ...]" including across line breaks. Non-greedy so
    /// two markers in one document don't collapse into one match.</summary>
    [GeneratedRegex(@"\[TODO:.*?\]", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex TodoRegex();

    /// <summary>HTML comments are the Markdown-native place for notes aimed at
    /// whoever edits this document - guidance about how to write it, not
    /// facts about the platform. They render invisibly, and they must not
    /// reach the model either, which would otherwise treat instructions
    /// meant for a human as context about Wasta.</summary>
    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex HtmlCommentRegex();

    /// <summary>A list item that held nothing but a TODO leaves a stray bullet.</summary>
    [GeneratedRegex(@"^[ \t]*[-*][ \t]*$", RegexOptions.Multiline)]
    private static partial Regex EmptyBulletRegex();

    [GeneratedRegex(@"\n{3,}")]
    private static partial Regex ExcessBlankLinesRegex();

    public static async Task<KnowledgeBase> LoadAsync(string configuredPath, CancellationToken ct)
    {
        var resolvedPath = PromptFile.ResolvePath(configuredPath);
        var stamp = File.GetLastWriteTimeUtc(resolvedPath);

        if (Cache.TryGetValue(resolvedPath, out var cached) && cached.Stamp == stamp)
        {
            return cached.Value;
        }

        var raw = await File.ReadAllTextAsync(resolvedPath, ct);
        var parsed = Parse(raw);

        Cache[resolvedPath] = (stamp, parsed);
        return parsed;
    }

    internal static KnowledgeBase Parse(string raw)
    {
        var todoCount = TodoRegex().Matches(raw).Count;

        var text = HtmlCommentRegex().Replace(raw, string.Empty);
        text = TodoRegex().Replace(text, string.Empty);
        text = EmptyBulletRegex().Replace(text, string.Empty);
        text = ExcessBlankLinesRegex().Replace(text, "\n\n");

        return new KnowledgeBase(text.Trim(), todoCount);
    }
}
