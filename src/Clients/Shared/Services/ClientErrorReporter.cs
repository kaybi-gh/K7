using System.Net.Http.Json;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Logging;

namespace K7.Clients.Shared.Services;

public sealed class ClientErrorReporter(
    IK7Snackbar snackbar,
    IK7ServerService serverService,
    IDeviceService deviceService,
    ILogger<ClientErrorReporter> logger) : IClientErrorReporter
{
    public void ReportError(Exception exception, string? context = null)
    {
        var message = context is not null
            ? $"{context}: {exception.Message}"
            : exception.Message;

        logger.LogError(exception, "Client error: {Context}", context ?? "unhandled");

        snackbar.Add(message, K7Severity.Error, "Error");

        _ = SendToServerAsync(exception, context);
    }

    private async Task SendToServerAsync(Exception exception, string? context)
    {
        try
        {
            var report = new
            {
                message = context is not null ? $"{context}: {exception.Message}" : exception.Message,
                source = exception.Source,
                stackTrace = exception.StackTrace,
                deviceId = deviceService.GetDeviceId()
            };

            await serverService.HttpClient.PostAsJsonAsync("/api/diagnostics/client-errors", report);
        }
        catch
        {
            // Best-effort reporting - don't crash on reporting failure
        }
    }
}
