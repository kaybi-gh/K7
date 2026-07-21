using K7.Clients.Shared.Interfaces;

namespace K7.Server.Web.Services;

internal sealed class ServerSideErrorReporter(ILogger<ServerSideErrorReporter> logger) : IClientErrorReporter
{
    public void ReportError(Exception exception, string? context = null, bool notifyUser = true)
    {
        logger.LogError(exception, "SSR error boundary: {Context}", context ?? "unhandled");
    }
}
