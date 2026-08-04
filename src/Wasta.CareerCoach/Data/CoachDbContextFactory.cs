using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Wasta.CareerCoach.Data;

/// <summary>Design-time only, used by `dotnet ef migrations add`. The host app supplies
/// the real connection string at runtime via AddCareerCoach(...).</summary>
public class CoachDbContextFactory : IDesignTimeDbContextFactory<CoachDbContext>
{
    public CoachDbContext CreateDbContext(string[] args)
    {
        var builder = new DbContextOptionsBuilder<CoachDbContext>();
        builder.UseNpgsql("Host=localhost;Database=wasta_design_time;Username=postgres;Password=postgres");
        return new CoachDbContext(builder.Options);
    }
}
