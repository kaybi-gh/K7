using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Notifications.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

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
            [FromServices] ClientErrorNotificationPublisher notificationPublisher,
            [FromServices] IApplicationDbContext db,
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

            string? deviceName = null;
            if (Guid.TryParse(report.DeviceId, out var deviceGuid))
            {
                deviceName = await db.Devices
                    .AsNoTracking()
                    .Where(d => d.Id == deviceGuid)
                    .Select(d => d.DeviceName)
                    .FirstOrDefaultAsync(cancellationToken);
            }

            try
            {
                await notificationPublisher.TryPublishAsync(
                    report.Message,
                    report.Source,
                    report.StackTrace,
                    report.DeviceId,
                    deviceName ?? report.DeviceId,
                    userId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to publish ClientErrorReportedEvent");
            }

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
