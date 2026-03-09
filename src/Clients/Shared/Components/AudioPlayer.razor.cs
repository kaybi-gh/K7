using K7.Clients.Shared.Domain.Interfaces;
using K7.Clients.Shared.Domain.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Components;

public partial class AudioPlayer : IAsyncDisposable
{
    private DotNetObjectReference<AudioPlayer>? _dotNetRef;
    private bool _isInitialized;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (AudioPlayerService.IsVisible && !_isInitialized)
        {
            _dotNetRef ??= DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("initAudioPlayer", _dotNetRef);
            _isInitialized = true;

            // Apply persisted volume state
            await JSRuntime.InvokeVoidAsync("audioSetVolume", AudioPlayerService.Volume);
            await JSRuntime.InvokeVoidAsync("audioSetMuted", AudioPlayerService.IsMuted);
        }
    }

    protected override void OnInitialized()
    {
        AudioPlayerService.PlayRequested += PlayAsync;
        AudioPlayerService.PauseRequested += PauseAsync;
        AudioPlayerService.StopRequested += StopAsync;
        AudioPlayerService.SeekRequested += SeekAsync;
        AudioPlayerService.MuteRequested += MuteAsync;
        AudioPlayerService.UnmuteRequested += UnmuteAsync;
        AudioPlayerService.VolumeChangeRequested += SetVolumeAsync;
        AudioPlayerService.SourceChanged += OnSourceChanged;
        AudioPlayerService.IsVisibleChanged += OnVisibilityChanged;
    }

    public async ValueTask DisposeAsync()
    {
        AudioPlayerService.PlayRequested -= PlayAsync;
        AudioPlayerService.PauseRequested -= PauseAsync;
        AudioPlayerService.StopRequested -= StopAsync;
        AudioPlayerService.SeekRequested -= SeekAsync;
        AudioPlayerService.MuteRequested -= MuteAsync;
        AudioPlayerService.UnmuteRequested -= UnmuteAsync;
        AudioPlayerService.VolumeChangeRequested -= SetVolumeAsync;
        AudioPlayerService.SourceChanged -= OnSourceChanged;
        AudioPlayerService.IsVisibleChanged -= OnVisibilityChanged;

        if (_isInitialized)
        {
            try
            {
                await JSRuntime.InvokeVoidAsync("disposeAudioPlayer");
            }
            catch (JSDisconnectedException) { }
            catch (ObjectDisposedException) { }
        }

        _dotNetRef?.Dispose();
        _dotNetRef = null;
    }

    [JSInvokable]
    public void OnTimeUpdated(double currentTime)
    {
        AudioPlayerService.CurrentTime = currentTime;
    }

    [JSInvokable]
    public void OnDurationChanged(double duration)
    {
        AudioPlayerService.Duration = duration;
    }

    [JSInvokable]
    public void OnBufferedUpdated(double bufferedEnd)
    {
        AudioPlayerService.BufferedTime = bufferedEnd;
    }

    [JSInvokable]
    public void OnVolumeChanged(double volume, bool muted)
    {
        AudioPlayerService.Volume = volume;
        AudioPlayerService.IsMuted = muted;
    }

    [JSInvokable]
    public void OnPlaybackStateChanged(string state)
    {
        AudioPlayerService.PlaybackState = state switch
        {
            "playing" => PlaybackState.Playing,
            "paused" => PlaybackState.Paused,
            "buffering" => PlaybackState.Buffering,
            _ => PlaybackState.Unknown
        };
    }

    [JSInvokable]
    public async Task OnTrackEnded()
    {
        await AudioPlayerService.OnTrackEndedAsync();
    }

    private async Task PlayAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioPlay");
    }

    private async Task PauseAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioPause");
    }

    private async Task StopAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioStop");
    }

    private async Task SeekAsync(double time)
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSeek", time);
    }

    private async Task MuteAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSetMuted", true);
    }

    private async Task UnmuteAsync()
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSetMuted", false);
    }

    private async Task SetVolumeAsync(double volume)
    {
        if (!_isInitialized) return;
        await JSRuntime.InvokeVoidAsync("audioSetVolume", volume);
    }

    private async void OnSourceChanged(PlayerSource source)
    {
        if (!_isInitialized || string.IsNullOrEmpty(source.Url)) return;
        await JSRuntime.InvokeVoidAsync("audioChangeSource", source.Url, source.MimeType);
        await InvokeAsync(StateHasChanged);
    }

    private async void OnVisibilityChanged()
    {
        await InvokeAsync(StateHasChanged);
    }
}
