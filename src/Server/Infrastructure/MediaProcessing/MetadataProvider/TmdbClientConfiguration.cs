using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TMDbLib.Client;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

/// <summary>
/// Lazily loads TMDb API configuration (image base URLs, etc.) without blocking DI construction.
/// </summary>
internal static class TmdbClientConfiguration
{
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public static async Task EnsureConfiguredAsync(TMDbClient client, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (client.HasConfig)
            return;

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (client.HasConfig)
                return;

            var config = await client.GetConfigAsync().ConfigureAwait(false);
            if (!client.HasConfig)
                client.SetConfig(config);
        }
        finally
        {
            Gate.Release();
        }
    }
}

internal sealed class TmdbClientConfigurationHostedService(
    TMDbClient client,
    ILogger<TmdbClientConfigurationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await TmdbClientConfiguration.EnsureConfiguredAsync(client, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "TMDb configuration warmup failed; metadata image URLs may be unavailable until the first successful refresh");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
