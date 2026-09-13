using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;

namespace K7.Clients.Shared.Services;

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
        _videoPlayer.IsVisibleChanged += OnVideoVisibilityChanged;
        _audioPlayer.SourceChanged += OnAudioSourceChanged;
        _audioPlayer.IsVisibleChanged += OnAudioVisibilityChanged;
    }

    private void OnVideoSourceChanged(PlayerSource source) => SwitchToVideo();

    private void OnVideoVisibilityChanged()
    {
        if (_videoPlayer.IsVisible)
            SwitchToVideo();
    }

    private void OnAudioSourceChanged(PlayerSource source) => SwitchToAudio();

    private void OnAudioVisibilityChanged()
    {
        if (_audioPlayer.IsVisible)
            SwitchToAudio();
    }

    private void SwitchToVideo()
    {
        SetActivePlayer(ActivePlayerType.Video);
        HideAudioIfVisible();
    }

    private void SwitchToAudio()
    {
        SetActivePlayer(ActivePlayerType.Audio);
        HideVideoIfVisible();
    }

    private void SetActivePlayer(ActivePlayerType player)
    {
        if (ActivePlayer == player)
            return;

        ActivePlayer = player;
        ActivePlayerChanged?.Invoke(player);
    }

    private void HideAudioIfVisible()
    {
        if (!_audioPlayer.IsVisible)
            return;

        _audioPlayer.Stop();
        _ = _audioPlayer.HideAsync();
    }

    private void HideVideoIfVisible()
    {
        if (!_videoPlayer.IsVisible)
            return;

        _videoPlayer.Stop();
        _ = _videoPlayer.HideAsync();
    }

    public void Dispose()
    {
        _videoPlayer.SourceChanged -= OnVideoSourceChanged;
        _videoPlayer.IsVisibleChanged -= OnVideoVisibilityChanged;
        _audioPlayer.SourceChanged -= OnAudioSourceChanged;
        _audioPlayer.IsVisibleChanged -= OnAudioVisibilityChanged;
    }
}
