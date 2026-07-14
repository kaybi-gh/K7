using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace K7.Server.Web.Infrastructure;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string PinVerifyPolicy = "pin-verify";

    public static IServiceCollection AddK7RateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(AuthPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy(PinVerifyPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    private static string ResolveClientPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
