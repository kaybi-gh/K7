using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Diagnostics;

public class ReportClientError : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapPost("/api/diagnostics/client-errors", async (
            [FromBody] ClientErrorReport report,
            [FromServices] ILogger<ReportClientError> logger,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var userId = httpContext.User.Identity?.Name ?? "anonymous";
            logger.LogError(
                "Client error from {UserId} (device {DeviceId}): {Message} | {Source} | {StackTrace}",
                userId,
                report.DeviceId,
                report.Message,
                report.Source,
                report.StackTrace);

            return Results.Ok();
        })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}

public sealed record ClientErrorReport(
    string Message,
    string? Source,
    string? StackTrace,
    string? DeviceId);
