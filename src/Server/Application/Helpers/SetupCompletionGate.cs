using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Waits until first-run setup is complete so background services do not run (or spam) before admin exists.
/// </summary>
public static class SetupCompletionGate
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);

    public static async Task WaitUntilCompletedAsync(
        IServiceScopeFactory scopeFactory,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var setupService = scope.ServiceProvider.GetRequiredService<ISetupService>();
            if (await setupService.IsSetupCompletedAsync(cancellationToken))
                return;

            logger.LogDebug("Waiting for first-run setup to complete before starting background work");
            await Task.Delay(PollInterval, cancellationToken);
        }
    }
}
