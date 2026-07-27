using K7.Shared.Interfaces;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Process-wide music-intelligence availability so track rows do not each hit the API.
/// </summary>
internal static class MusicIntelligenceAvailabilityCache
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool? _available;

    public static async Task<bool> GetAsync(
        IServerPreferencesService serverPreferences,
        CancellationToken cancellationToken = default)
    {
        if (_available is not null)
            return _available.Value;

        await Gate.WaitAsync(cancellationToken);
        try
        {
            if (_available is not null)
                return _available.Value;

            try
            {
                var status = await serverPreferences.GetMusicIntelligenceStatusAsync(cancellationToken);
                _available = status.IsAvailable;
            }
            catch
            {
                _available = false;
            }

            return _available.Value;
        }
        finally
        {
            Gate.Release();
        }
    }
}
