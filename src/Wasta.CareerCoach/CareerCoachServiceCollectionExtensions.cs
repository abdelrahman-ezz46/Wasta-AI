using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wasta.Ai;
using Wasta.CareerCoach.BackgroundJobs;
using Wasta.CareerCoach.Data;
using Wasta.CareerCoach.Services;

namespace Wasta.CareerCoach;

public static class CareerCoachServiceCollectionExtensions
{
    /// <summary>
    /// Wires up the AI Career Coach module: DbContext, provider chain,
    /// generation service, and the two background services that generate
    /// and retry plans. You must additionally register your own
    /// implementations of IAssessmentDataProvider, ICurrentStudentAccessor,
    /// and IAuditLogWriter (see Domain/AssessmentDataContracts.cs and
    /// Domain/IAuditLogWriter.cs) against your real app before this will
    /// resolve at runtime.
    /// </summary>
    public static IServiceCollection AddCareerCoach(this IServiceCollection services, IConfiguration configuration, string coachConnectionString)
        => services.AddCareerCoach(configuration, options => options.UseNpgsql(coachConnectionString));

    /// <summary>Overload letting the host pick its own EF provider - Npgsql in
    /// production, in-memory for a dev host or tests.</summary>
    public static IServiceCollection AddCareerCoach(
        this IServiceCollection services, IConfiguration configuration, Action<DbContextOptionsBuilder> configureDb)
    {
        services.AddWastaAi(configuration);
        services.Configure<CoachOptions>(configuration.GetSection(CoachOptions.SectionName));

        services.AddDbContext<CoachDbContext>(configureDb);

        services.AddScoped<CoachGenerationService>();
        services.AddScoped<CoachPlanTrigger>();

        services.AddSingleton<CoachGenerationQueue>();
        services.AddHostedService<CoachGenerationWorker>();
        services.AddHostedService<CoachSweeperService>();

        return services;
    }
}
