using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;

namespace K7.Clients.Web.Services;

public class MediaPlayerService : IMediaPlayerService
{
    private readonly IPlayerService _videoPlayer;
    private readonly IAudioPlayerService _audioPlayer;

    public ActivePlayerType ActivePlayer { get; private set; } = ActivePlayerType.None;
    public event Action<ActivePlayerType>? ActivePlayerChanged;

    public MediaPlayerService(IPlayerService videoPlayer, IAudioPlayerService audioPlayer)
    {
        _videoPlayer = videoPlayer;
        _audioPlayer = audioPlayer;

        _videoPlayer.SourceChanged += OnVideoSourceChanged;
        _audioPlayer.SourceChanged += OnAudioSourceChanged;
    }

    private void OnVideoSourceChanged(PlayerSource source)
    {
        if (ActivePlayer == ActivePlayerType.Video) return;

        _audioPlayer.Stop();
        _ = _audioPlayer.HideAsync();

        ActivePlayer = ActivePlayerType.Video;
        ActivePlayerChanged?.Invoke(ActivePlayerType.Video);
    }

    private void OnAudioSourceChanged(PlayerSource source)
    {
        if (ActivePlayer == ActivePlayerType.Audio) return;

        _videoPlayer.Stop();
        _ = _videoPlayer.HideAsync();

        ActivePlayer = ActivePlayerType.Audio;
        ActivePlayerChanged?.Invoke(ActivePlayerType.Audio);
    }

    public void Dispose()
    {
        _videoPlayer.SourceChanged -= OnVideoSourceChanged;
        _audioPlayer.SourceChanged -= OnAudioSourceChanged;
    }
}
