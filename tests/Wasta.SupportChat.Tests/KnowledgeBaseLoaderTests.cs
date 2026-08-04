using Wasta.SupportChat.Services;

namespace Wasta.SupportChat.Tests;

/// <summary>
/// The knowledge base is pasted wholesale into the system prompt, so
/// anything left in it is something the model can quote to a student.
/// Unfinished "[TODO: ...]" drafts are normal while the document is being
/// written - they just must never survive as far as the prompt.
/// </summary>
public class KnowledgeBaseLoaderTests
{
    [Fact]
    public void Parse_StripsTodoMarkers_AndCountsThem()
    {
        var raw = """
            ## Accounts

            - Students sign in with an email address.
            - [TODO: how does someone reset a password?]
            - [TODO: what is the account deletion process?]
            """;

        var result = KnowledgeBaseLoader.Parse(raw);

        Assert.Equal(2, result.UnresolvedTodoCount);
        Assert.DoesNotContain("TODO", result.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reset a password", result.Text);
        Assert.Contains("Students sign in with an email address.", result.Text);
    }

    [Fact]
    public void Parse_StripsTodoSpanningMultipleLines()
    {
        var raw = """
            ## Employers

            - Employers search the platform.
            - [TODO: what actually happens when an employer unlocks a
              profile - what do they see, is the student notified, is
              there a cost?]
            """;

        var result = KnowledgeBaseLoader.Parse(raw);

        Assert.Equal(1, result.UnresolvedTodoCount);
        Assert.DoesNotContain("unlocks a", result.Text);
        Assert.DoesNotContain("is there a cost", result.Text);
        Assert.Contains("Employers search the platform.", result.Text);
    }

    [Fact]
    public void Parse_WithTwoTodosOnOneLine_DoesNotSwallowTheTextBetween()
    {
        var raw = "Keep this. [TODO: first gap] Also keep this. [TODO: second gap] And this.";

        var result = KnowledgeBaseLoader.Parse(raw);

        Assert.Equal(2, result.UnresolvedTodoCount);
        Assert.Contains("Keep this.", result.Text);
        Assert.Contains("Also keep this.", result.Text);
        Assert.Contains("And this.", result.Text);
    }

    [Fact]
    public void Parse_RemovesBulletsLeftEmptyByStripping()
    {
        var raw = """
            - Real content here.
            - [TODO: unfinished]
            - More real content.
            """;

        var result = KnowledgeBaseLoader.Parse(raw);

        Assert.DoesNotContain("\n-\n", result.Text);
        Assert.DoesNotContain("- \n", result.Text);
        Assert.Contains("Real content here.", result.Text);
        Assert.Contains("More real content.", result.Text);
    }

    [Fact]
    public void Parse_CleanDocument_IsLeftIntactAndReportsNoTodos()
    {
        var raw = """
            ## The Wasta Score

            - The score is deterministic and rule-based.
            - The method is published.
            """;

        var result = KnowledgeBaseLoader.Parse(raw);

        Assert.Equal(0, result.UnresolvedTodoCount);
        Assert.Contains("deterministic and rule-based", result.Text);
        Assert.Contains("The method is published.", result.Text);
    }

    [Fact]
    public void Parse_ShippedKnowledgeBase_HasItsTodosStripped()
    {
        // Guards the real file, not a fixture: if someone adds a TODO to the
        // shipped knowledge base, it still must not reach the prompt.
        var path = Path.Combine(AppContext.BaseDirectory, "Knowledge", "PlatformKnowledge.v1.md");
        Assert.True(File.Exists(path), $"Expected the knowledge base to ship next to the assembly at {path}");

        var result = KnowledgeBaseLoader.Parse(File.ReadAllText(path));

        Assert.DoesNotContain("TODO", result.Text, StringComparison.OrdinalIgnoreCase);
    }
}
