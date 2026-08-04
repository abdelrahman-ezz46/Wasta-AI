using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Wasta.Ai;

public static class AiServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared Groq/Gemini provider chain, bound to the "Ai"
    /// config section. Safe to call from multiple feature modules (Career
    /// Coach, Support Chat, ...) - uses TryAdd so calling it more than once
    /// never creates duplicate provider registrations. A duplicate would
    /// make AiProviderChain throw when it builds its name-keyed dictionary.
    /// </summary>
    public static IServiceCollection AddWastaAi(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));

        services.AddHttpClient("ai-groq");
        services.AddHttpClient("ai-gemini");

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAiProvider, GroqProvider>());
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IAiProvider, GeminiProvider>());
        services.TryAddSingleton<AiProviderChain>();

        return services;
    }
}
