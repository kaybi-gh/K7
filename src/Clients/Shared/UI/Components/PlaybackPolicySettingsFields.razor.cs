using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class PlaybackPolicySettingsFields
{
    [Parameter] public VideoPlaybackPolicySettingsDto? VideoPolicy { get; set; }
    [Parameter] public EventCallback<VideoPlaybackPolicySettingsDto> VideoPolicyChanged { get; set; }
    [Parameter] public AudioPlaybackPolicySettingsDto? AudioPolicy { get; set; }
    [Parameter] public EventCallback<AudioPlaybackPolicySettingsDto> AudioPolicyChanged { get; set; }

    private async Task OnVideoChanged(Func<VideoPlaybackPolicySettingsDto, VideoPlaybackPolicySettingsDto> update)
    {
        if (VideoPolicy is null)
            return;

        var updated = update(VideoPolicy);
        VideoPolicy = updated;
        await VideoPolicyChanged.InvokeAsync(updated);
        StateHasChanged();
    }

    private async Task OnAudioChanged(Func<AudioPlaybackPolicySettingsDto, AudioPlaybackPolicySettingsDto> update)
    {
        if (AudioPolicy is null)
            return;

        var updated = update(AudioPolicy);
        AudioPolicy = updated;
        await AudioPolicyChanged.InvokeAsync(updated);
        StateHasChanged();
    }

    private static string FormatPercent(int value) => $"{value} %";

    private static string FormatSeconds(int value) => $"{value} s";
}
