using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace K7.Server.Web.Infrastructure;

public static class RateLimitingExtensions
{
    public const string AuthPolicy = "auth";
    public const string PinVerifyPolicy = "pin-verify";
    public const string FederationInvitationPolicy = "federation-invitation";
    public const string ShareOpenPolicy = "share-open";
    public const string ShareStreamPolicy = "share-stream";
    public const string ShareVisitorPolicy = "share-visitor";

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

            // Anonymous share link lookups and password attempts.
            options.AddPolicy(ShareOpenPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            // Anonymous visitors: each new stream session can start a transcode.
            options.AddPolicy(ShareStreamPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveShareVisitorPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy(ShareVisitorPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveShareVisitorPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 120,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    }));

            options.AddPolicy(FederationInvitationPolicy, context =>
                RateLimitPartition.GetFixedWindowLimiter(
                    ResolveClientPartitionKey(context),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 10,
                        Window = TimeSpan.FromMinutes(10),
                        QueueLimit = 0
                    }));
        });

        return services;
    }

    // One bucket per visitor (its session cookie), so visitors behind the same proxy or
    // address do not throttle each other.
    private static string ResolveShareVisitorPartitionKey(HttpContext context) =>
        context.Request.Cookies.TryGetValue(ShareSessionCookie.Name, out var cookie) && !string.IsNullOrEmpty(cookie)
            ? "share:" + K7.Server.Application.Common.Security.ShareTokenHelper.Hash(cookie)
            : ResolveClientPartitionKey(context);

    private static string ResolveClientPartitionKey(HttpContext context) =>
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
