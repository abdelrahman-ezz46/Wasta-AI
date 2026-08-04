using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Wasta.Ai;
using Wasta.SupportChat.Data;
using Wasta.SupportChat.Domain;
using Wasta.SupportChat.Services;
using Wasta.SupportChat.Tests.Fakes;

namespace Wasta.SupportChat.Tests;

/// <summary>
/// Covers the two new capabilities: cross-visit memory for logged-in
/// students, and job-listing recommendations. The isolation tests here are
/// the load-bearing ones - cross-session memory is exactly the kind of
/// feature that silently leaks one person's data into another person's
/// conversation if the query filter is ever loosened.
/// </summary>
public class SupportChatMemoryAndJobsTests : IDisposable
{

    private static ChatCaller CallerFor(ChatSession s) => new(s.StudentId, s.VisitorId);
    private static readonly ChatCaller AnyCaller = new(null, "any-visitor");
    private readonly string _promptPath;
    private readonly string _knowledgePath;

    public SupportChatMemoryAndJobsTests()
    {
        _promptPath = Path.Combine(Path.GetTempPath(), $"chat-prompt-{Guid.NewGuid():N}.txt");
        _knowledgePath = Path.Combine(Path.GetTempPath(), $"chat-knowledge-{Guid.NewGuid():N}.md");
        File.WriteAllText(_promptPath, "You are the Wasta support assistant. PLATFORM_KNOWLEDGE:");
        File.WriteAllText(_knowledgePath, "Students take an assessment and get a Wasta Score.");
    }

    public void Dispose()
    {
        if (File.Exists(_promptPath)) File.Delete(_promptPath);
        if (File.Exists(_knowledgePath)) File.Delete(_knowledgePath);
    }

    private SupportChatDbContext NewDb(string? name = null)
    {
        var options = new DbContextOptionsBuilder<SupportChatDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new SupportChatDbContext(options);
    }

    private SupportChatService BuildService(
        SupportChatDbContext db,
        FakeAiProvider provider,
        int crossSessionMemoryTurns = 12,
        IJobListingProvider? jobListingProvider = null)
    {
        var aiOptions = Options.Create(new AiOptions { Chain = [provider.Name] });
        var chatOptions = Options.Create(new SupportChatOptions
        {
            PromptPath = _promptPath,
            KnowledgePath = _knowledgePath,
            MinSecondsBetweenMessages = 0,
            CrossSessionMemoryTurns = crossSessionMemoryTurns,
        });
        var chain = new AiProviderChain([provider], aiOptions, NullLogger<AiProviderChain>.Instance);
        return new SupportChatService(
            db, chain, jobListingProvider ?? new NullJobListingProvider(), chatOptions, NullLogger<SupportChatService>.Instance);
    }

    // --- Cross-session memory ---

    [Fact]
    public async Task ReturningStudent_NewSession_IsSeededWithEarlierSessionHistory()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq")
            .EnqueueResponse("The Wasta Score is deterministic.")
            .EnqueueResponse("Sure, happy to continue.");
        var service = BuildService(db, provider);

        var firstSession = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(firstSession.PublicId, CallerFor(firstSession), "What is the Wasta Score?", CancellationToken.None);

        var secondSession = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(secondSession.PublicId, CallerFor(secondSession), "Can you continue helping me?", CancellationToken.None);

        Assert.Contains(provider.LastTurns!, t => t.Role == "user" && t.Content == "What is the Wasta Score?");
        Assert.Contains(provider.LastTurns!, t => t.Role == "assistant" && t.Content == "The Wasta Score is deterministic.");
    }

    [Fact]
    public async Task AnonymousVisitor_NewSession_NeverGetsCrossSessionMemory_EvenWithSameVisitorId()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq")
            .EnqueueResponse("First answer.")
            .EnqueueResponse("Second answer.");
        var service = BuildService(db, provider);

        var firstSession = (await service.CreateSessionAsync(studentId: null, visitorId: "same-visitor", CancellationToken.None))!;
        await service.SendMessageAsync(firstSession.PublicId, CallerFor(firstSession), "A private first question", CancellationToken.None);

        var secondSession = (await service.CreateSessionAsync(studentId: null, visitorId: "same-visitor", CancellationToken.None))!;
        await service.SendMessageAsync(secondSession.PublicId, CallerFor(secondSession), "A new question", CancellationToken.None);

        Assert.DoesNotContain(provider.LastTurns!, t => t.Content == "A private first question");
        Assert.Single(provider.LastTurns!);
    }

    [Fact]
    public async Task DifferentStudents_NeverShareCrossSessionMemory()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq")
            .EnqueueResponse("Answer for student A.")
            .EnqueueResponse("Answer for student B.");
        var service = BuildService(db, provider);

        var studentASession = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(studentASession.PublicId, CallerFor(studentASession), "Student A's secret question", CancellationToken.None);

        var studentBSession = (await service.CreateSessionAsync(studentId: 2, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(studentBSession.PublicId, CallerFor(studentBSession), "Student B's question", CancellationToken.None);

        Assert.DoesNotContain(provider.LastTurns!, t => t.Content.Contains("Student A"));
    }

    [Fact]
    public async Task CrossSessionMemory_IsBoundedByConfiguredTurnCount()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq")
            .EnqueueResponse("A1").EnqueueResponse("A2").EnqueueResponse("A3")
            .EnqueueResponse("Final answer");
        var service = BuildService(db, provider, crossSessionMemoryTurns: 2);

        var firstSession = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(firstSession.PublicId, CallerFor(firstSession), "Q1", CancellationToken.None);
        await service.SendMessageAsync(firstSession.PublicId, CallerFor(firstSession), "Q2", CancellationToken.None);
        await service.SendMessageAsync(firstSession.PublicId, CallerFor(firstSession), "Q3", CancellationToken.None);

        var secondSession = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(secondSession.PublicId, CallerFor(secondSession), "New question", CancellationToken.None);

        // Only the 2 most recent prior messages (CrossSessionMemoryTurns=2) plus the new one.
        Assert.Equal(3, provider.LastTurns!.Count);
        Assert.DoesNotContain(provider.LastTurns!, t => t.Content is "Q1" or "A1");
    }

    // --- Job listings ---

    [Fact]
    public async Task SystemPrompt_IncludesJobListings_WhenProviderReturnsSome()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("There's a Data Analyst role open.");
        var jobs = new FakeJobListingProvider(
            new JobListing("Data Analyst", "Acme Corp", "Data & AI", ["SQL", "Python"], "Riyadh", "https://example.com/jobs/1"));
        var service = BuildService(db, provider, jobListingProvider: jobs);

        var session = (await service.CreateSessionAsync(studentId: 1, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, CallerFor(session), "Any data jobs open?", CancellationToken.None);

        Assert.Contains("OPEN_OPPORTUNITIES", provider.LastSystemPrompt);
        Assert.Contains("Data Analyst", provider.LastSystemPrompt);
        Assert.Contains("https://example.com/jobs/1", provider.LastSystemPrompt);
    }

    [Fact]
    public async Task SystemPrompt_OmitsOpenOpportunitiesSection_WhenNoListings()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("Here's how scoring works.");
        var service = BuildService(db, provider); // default NullJobListingProvider

        var session = (await service.CreateSessionAsync(studentId: null, visitorId: "v1", CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, CallerFor(session), "How does scoring work?", CancellationToken.None);

        Assert.DoesNotContain("OPEN_OPPORTUNITIES", provider.LastSystemPrompt);
    }

    [Fact]
    public async Task JobListingProvider_ReceivesStudentIdForPersonalization()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("Some jobs for you.");
        var jobs = new FakeJobListingProvider();
        var service = BuildService(db, provider, jobListingProvider: jobs);

        var session = (await service.CreateSessionAsync(studentId: 42, visitorId: null, CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, CallerFor(session), "Any jobs for me?", CancellationToken.None);

        Assert.Equal(42, jobs.LastStudentId);
    }

    [Fact]
    public async Task JobListingProvider_ReceivesNullStudentIdForAnonymousVisitor()
    {
        using var db = NewDb();
        var provider = new FakeAiProvider("groq").EnqueueResponse("Some general jobs.");
        var jobs = new FakeJobListingProvider();
        var service = BuildService(db, provider, jobListingProvider: jobs);

        var session = (await service.CreateSessionAsync(studentId: null, visitorId: "anon-1", CancellationToken.None))!;
        await service.SendMessageAsync(session.PublicId, CallerFor(session), "Any jobs?", CancellationToken.None);

        Assert.Null(jobs.LastStudentId);
    }
}
