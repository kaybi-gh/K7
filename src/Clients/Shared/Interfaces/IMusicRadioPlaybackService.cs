using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IMusicRadioPlaybackService
{
    bool IsLoading { get; }
    string? LoadingPresetTitle { get; }
    event Action? LoadingStateChanged;

    Task<bool> StartAsync(MusicRadioRequest request, CancellationToken cancellationToken = default);
    void StopRefill();
}
