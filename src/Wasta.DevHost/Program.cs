using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Wasta.Ai;
using Wasta.CareerCoach;
using Wasta.CareerCoach.Api;
using Wasta.CareerCoach.Domain;
using Wasta.CareerCoach.Services;
using Wasta.DevHost.Adapters;
using Wasta.SupportChat;
using Wasta.SupportChat.Api;
using Wasta.SupportChat.Domain;
using CoachDomain = Wasta.CareerCoach.Domain;
using ChatDomain = Wasta.SupportChat.Domain;

var builder = WebApplication.CreateBuilder(args);

// The dev auth handler trusts client-supplied headers. Refuse to start
// anywhere but Development so this can never be deployed by accident.
if (!builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Wasta.DevHost is a development harness only. It fakes authentication from request headers "
        + "and must never run outside the Development environment.");
}

builder.Services
    .AddAuthentication(DevAuthHandler.SchemeName)
    .AddScheme<AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, _ => { });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy("StudentOnly", policy => policy.RequireRole("Student"))
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

// Defaults to in-memory EF so `dotnet run` needs no database at all. Set a
// ConnectionStrings:Wasta value to run against real Postgres instead -
// worth doing before trusting anything, because the in-memory provider
// ignores column types entirely, so jsonb columns and unique indexes are
// never actually exercised by it.
var connectionString = builder.Configuration.GetConnectionString("Wasta");
var usePostgres = !string.IsNullOrWhiteSpace(connectionString);

void ConfigureDb(DbContextOptionsBuilder db, string inMemoryName)
{
    if (usePostgres)
    {
        db.UseNpgsql(connectionString);
    }
    else
    {
        db.UseInMemoryDatabase(inMemoryName);
    }
}

builder.Services.AddCareerCoach(builder.Configuration, db => ConfigureDb(db, "wasta-coach-dev"));
builder.Services.AddSupportChat(builder.Configuration, db => ConfigureDb(db, "wasta-chat-dev"));

// The five ports the modules leave for the host to implement.
builder.Services.AddSingleton<DemoAssessmentStore>();
builder.Services.AddSingleton<IAssessmentDataProvider>(sp => sp.GetRequiredService<DemoAssessmentStore>());
builder.Services.AddSingleton<CoachDomain.ICurrentStudentAccessor, DevCurrentStudentAccessor>();
builder.Services.AddSingleton<ChatDomain.ICurrentStudentAccessor, DevCurrentStudentAccessor>();
builder.Services.AddSingleton<IAuditLogWriter, ConsoleAuditLogWriter>();
builder.Services.AddSingleton<IJobListingProvider, DemoJobListingProvider>();

// Last in the chain: only reached when no real provider is configured.
builder.Services.AddSingleton<IAiProvider, DevEchoProvider>();

var app = builder.Build();

app.Logger.LogInformation("Storage: {Storage}", usePostgres ? "PostgreSQL" : "in-memory (no column types enforced)");

app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapCareerCoachEndpoints();
app.MapSupportChatEndpoints();

// Stands in for the host app's real assessment submit flow. The only line
// that belongs to the Career Coach is the EnqueueGenerationAsync call - and
// note it is awaited but does no AI work, so the response returns
// immediately while generation happens in the background.
app.MapPost("/api/dev/assessments/submit", async (
    SubmitAssessmentRequest request,
    DemoAssessmentStore store,
    CoachPlanTrigger trigger,
    CancellationToken ct) =>
{
    var sections = request.Sections
        .Select(s => new SectionScoreData(s.Name, s.Percent))
        .ToList();

    // Optional: lets the guardrail script plant untrusted text (e.g. a
    // prompt-injection attempt) in the student's profile before generating.
    if (request.Skills is not null || request.ProjectTitles is not null)
    {
        store.SetStudentContext(request.StudentId, new StudentContextData(
            Skills: request.Skills ?? [],
            ProjectTitles: request.ProjectTitles ?? [],
            GraduationYear: request.GraduationYear));
    }

    var attempt = store.RecordAttempt(request.StudentId, sections);

    await trigger.EnqueueGenerationAsync(attempt.StudentId, attempt.AttemptId, attempt.ScoreId, ct);

    var overall = sections.Count == 0 ? 0 : (int)Math.Round(sections.Average(s => s.Percent));
    return Results.Ok(new
    {
        attemptId = attempt.AttemptId,
        scoreId = attempt.ScoreId,
        overallPercent = overall,
        sections = sections.Select(s => new { s.Name, s.Percent }),
    });
});

app.MapGet("/api/dev/health", () => Results.Ok(new { status = "ok" }));

app.Run();

internal sealed record SubmitAssessmentRequest(
    int StudentId,
    List<SubmitSection> Sections,
    List<string>? Skills = null,
    List<string>? ProjectTitles = null,
    int? GraduationYear = null);

internal sealed record SubmitSection(string Name, int Percent);
