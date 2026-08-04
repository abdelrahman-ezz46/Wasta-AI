using Wasta.SupportChat.Domain;

namespace Wasta.DevHost.Adapters;

/// <summary>
/// Stands in for the real job-listings source. Note it personalizes by
/// studentId - that's the extension point where your implementation would
/// match on track, skills, or score band. Anonymous visitors get the
/// general list.
/// </summary>
public class DemoJobListingProvider : IJobListingProvider
{
    private static readonly JobListing[] All =
    [
        new("Junior Data Analyst", "Nile Analytics", "Data & AI", ["SQL", "Python", "dashboards"], "Cairo", "https://example.com/jobs/nile-analyst"),
        new("Data Engineering Intern", "Delta Logistics", "Data & AI", ["SQL", "ETL", "Airflow"], "Alexandria (hybrid)", "https://example.com/jobs/delta-intern"),
        new("ML Intern", "Horus Health", "Data & AI", ["Python", "scikit-learn", "statistics"], "Remote", "https://example.com/jobs/horus-ml"),
        new("Frontend Developer", "Cedar Apps", "Software Engineering", ["React", "TypeScript"], "Cairo", "https://example.com/jobs/cedar-frontend"),
    ];

    public Task<IReadOnlyList<JobListing>> GetOpenListingsAsync(int? studentId, int maxResults, CancellationToken ct)
    {
        // Student 2 is seeded as a frontend student, so they see a different
        // slice - enough to show personalization is real and driven by the
        // host, not by the chat module.
        var pool = studentId == 2
            ? All.Where(j => j.Track == "Software Engineering")
            : All.Where(j => j.Track == "Data & AI");

        return Task.FromResult<IReadOnlyList<JobListing>>(pool.Take(maxResults).ToList());
    }
}
