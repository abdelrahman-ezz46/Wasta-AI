namespace Wasta.SupportChat.Domain;

/// <summary>Deliberately thin - only what's useful to mention in a chat reply.
/// Keep it small; every field here costs tokens on every turn it's offered.</summary>
public sealed record JobListing(
    string Title,
    string EmployerName,
    string? Track,
    IReadOnlyList<string> Skills,
    string? Location,
    string Url);

/// <summary>
/// Bridge into the host app's real job listings. Implement against your
/// actual data and register it in DI BEFORE calling AddSupportChat (see
/// SupportChatServiceCollectionExtensions - it only registers a no-op
/// fallback via TryAdd, so your registration must come first to win).
///
/// studentId lets your implementation personalize results (e.g. by the
/// student's track or skills) using data this module never sees directly -
/// Support Chat has no coupling to the assessment/scoring domain. Return an
/// empty list for anonymous visitors, or general/popular listings if you'd
/// rather show something.
/// </summary>
public interface IJobListingProvider
{
    Task<IReadOnlyList<JobListing>> GetOpenListingsAsync(int? studentId, int maxResults, CancellationToken ct);
}

/// <summary>Default when no real provider is wired - job recommendations are an
/// enhancement, not a requirement, so this lets the chatbot work before
/// (or without) that integration existing.</summary>
public sealed class NullJobListingProvider : IJobListingProvider
{
    public Task<IReadOnlyList<JobListing>> GetOpenListingsAsync(int? studentId, int maxResults, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<JobListing>>([]);
}
