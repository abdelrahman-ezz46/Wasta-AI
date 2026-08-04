using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

namespace Wasta.SupportChat.Api;

/// <summary>
/// Per-IP limits on the public chat endpoints.
///
/// The per-session caps in SupportChatService (message count, throttle) are
/// necessary but not sufficient on their own: session creation is
/// unauthenticated, so anything enforced per session is bypassed by simply
/// creating a new one per message. These limits are partitioned by client
/// IP instead, which is what actually bounds AI spend.
///
/// Behind a load balancer or CDN, RemoteIpAddress is the proxy unless
/// forwarded headers are configured - wire up UseForwardedHeaders in the
/// host app or every visitor shares one bucket.
/// </summary>
public static class SupportChatRateLimiting
{
    public const string SessionCreationPolicy = "wasta-chat-session-creation";
    public const string MessagePolicy = "wasta-chat-messages";

    public static IServiceCollection AddSupportChatRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Sessions are cheap to create but each one is a fresh AI budget,
            // so this is the tighter of the two.
            options.AddPolicy(SessionCreationPolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0,
                    }));

            // Generous enough for real conversation, far below what a script
            // would need to run up a bill.
            options.AddPolicy(MessagePolicy, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: ClientKey(httpContext),
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                    }));
        });

        return services;
    }

    private static string ClientKey(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
