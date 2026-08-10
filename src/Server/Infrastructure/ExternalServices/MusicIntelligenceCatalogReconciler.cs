using K7.Server.Application.Common.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Infrastructure.ExternalServices;

/// <summary>
/// Debounces AudioMuse align + catalogue cleaning after music Guid churn.
/// Align remaps server item ids against the shared catalogue when fingerprints match;
/// cleaning removes mappings/catalogue rows that no longer exist on any server.
/// </summary>
public sealed class MusicIntelligenceCatalogReconciler(
    IServiceScopeFactory scopeFactory,
    ILogger<MusicIntelligenceCatalogReconciler> logger) : IMusicIntelligenceCatalogReconciler
{
    private static readonly TimeSpan Debounce = TimeSpan.FromSeconds(30);

    private readonly object _gate = new();
    private CancellationTokenSource? _debounceCts;
    private int _pending;

    public void RequestReconcile()
    {
        lock (_gate)
        {
            _pending++;
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            var token = _debounceCts.Token;
            _ = RunAfterDebounceAsync(token);
        }
    }

    private async Task RunAfterDebounceAsync(CancellationToken debounceToken)
    {
        try
        {
            await Task.Delay(Debounce, debounceToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        int batch;
        lock (_gate)
        {
            if (debounceToken.IsCancellationRequested)
                return;

            batch = _pending;
            _pending = 0;
        }

        if (batch <= 0)
            return;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var adapter = scope.ServiceProvider.GetRequiredService<AudioMuseMusicIntelligenceAdapter>();
            var settings = await adapter.GetSettingsAsync(CancellationToken.None);
            if (settings is not { Enabled: true } || string.IsNullOrWhiteSpace(settings.BaseUrl))
                return;

            logger.LogInformation(
                "Reconciling AudioMuse catalogue after {RequestCount} music identity change(s)",
                batch);

            await adapter.StartServerAlignAsync(CancellationToken.None);
            await adapter.StartCleaningAsync(cleanCatalogue: true, CancellationToken.None);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "AudioMuse catalogue reconcile failed");
        }
    }
}
