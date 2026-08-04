using Wasta.SupportChat.Domain;

namespace Wasta.SupportChat.Tests.Fakes;

public class FakeJobListingProvider : IJobListingProvider
{
    private readonly IReadOnlyList<JobListing> _listings;

    public FakeJobListingProvider(params JobListing[] listings) => _listings = listings;

    public int? LastStudentId { get; private set; }

    public Task<IReadOnlyList<JobListing>> GetOpenListingsAsync(int? studentId, int maxResults, CancellationToken ct)
    {
        LastStudentId = studentId;
        return Task.FromResult(_listings.Take(maxResults).ToList() as IReadOnlyList<JobListing>);
    }
}
