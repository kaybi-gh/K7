using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Interfaces;

public interface IMusicRadioPlaybackService
{
    bool IsLoading { get; }
    string? LoadingPresetTitle { get; }
    event Action? LoadingStateChanged;

    Task<bool> StartAsync(MusicRadioRequest request, CancellationToken cancellationToken = default);
    void StopRefill();
    MusicRadioRequest? ActiveRequest { get; }
    void AttachWithoutPlaying(MusicRadioRequest request);
    void BeginRefill();

    /// <summary>
    /// Appends one more batch of the active radio to the queue (end of queue reached before
    /// the refill loop caught up). False when no radio is active or it has nothing left.
    /// </summary>
    Task<bool> ExtendAsync(CancellationToken cancellationToken = default);
}
