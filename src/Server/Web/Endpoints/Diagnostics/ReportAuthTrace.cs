using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using K7.Server.Web.Infrastructure;

namespace K7.Server.Web.Endpoints.Diagnostics;

public class ReportAuthTrace : IEndpoint
{
    private const int MaxFieldLength = 200;

    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/diagnostics/auth-trace", (
            [FromBody] AuthTraceReport? report,
            [FromServices] ILogger<ReportAuthTrace> logger) =>
        {
            if (report is null || string.IsNullOrWhiteSpace(report.Stage))
                return Results.BadRequest();

            logger.LogInformation(
                "Native auth {Stage} device {DeviceId}: {Detail}",
                Truncate(report.Stage),
                Truncate(report.DeviceId) ?? "anonymous",
                Truncate(report.Detail) ?? "-");

            return Results.Ok();
        })
        .AllowAnonymous()
        .RequireRateLimiting(RateLimitingExtensions.AuthPolicy)
        .WithName(type.Name)
        .WithTags(groupName);
    }

    private static string? Truncate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim().Replace('\r', ' ').Replace('\n', ' ');
        return trimmed.Length <= MaxFieldLength ? trimmed : trimmed[..MaxFieldLength];
    }
}

public sealed record AuthTraceReport(string Stage, string? Detail, string? DeviceId);
