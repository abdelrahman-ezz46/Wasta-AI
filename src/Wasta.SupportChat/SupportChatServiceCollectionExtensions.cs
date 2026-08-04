using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Wasta.Ai;
using Wasta.SupportChat.Api;
using Wasta.SupportChat.Data;
using Wasta.SupportChat.Domain;
using Wasta.SupportChat.Services;

namespace Wasta.SupportChat;

public static class SupportChatServiceCollectionExtensions
{
    /// <summary>
    /// Wires up the Support Chatbot module: DbContext and the chat service.
    /// You must additionally register your own implementation of
    /// ICurrentStudentAccessor (see Domain/ICurrentStudentAccessor.cs)
    /// before this will resolve at runtime. Shares the Groq/Gemini provider
    /// chain with AddCareerCoach via AddWastaAi - calling both is safe.
    ///
    /// Job recommendations are optional: register your own
    /// IJobListingProvider (see Domain/JobListing.cs) BEFORE calling this,
    /// and it wins over the no-op default. Without one, the chatbot still
    /// works, it just never has listings to offer.
    /// </summary>
    public static IServiceCollection AddSupportChat(this IServiceCollection services, IConfiguration configuration, string chatConnectionString)
        => services.AddSupportChat(configuration, options => options.UseNpgsql(chatConnectionString));

    /// <summary>Overload letting the host pick its own EF provider - Npgsql in
    /// production, in-memory for a dev host or tests.</summary>
    public static IServiceCollection AddSupportChat(
        this IServiceCollection services, IConfiguration configuration, Action<DbContextOptionsBuilder> configureDb)
    {
        services.AddWastaAi(configuration);
        services.Configure<SupportChatOptions>(configuration.GetSection(SupportChatOptions.SectionName));

        services.AddDbContext<SupportChatDbContext>(configureDb);

        services.AddSupportChatRateLimiting();

        services.TryAddSingleton<IJobListingProvider, NullJobListingProvider>();
        services.AddScoped<SupportChatService>();

        return services;
    }
}
