using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class RemoteControlPanel : ComponentBase, IDisposable
{
    [Parameter] public EventCallback OnResumeRequested { get; set; }

    protected override void OnInitialized()
    {
        Remote.StateChanged += OnStateChanged;
        Remote.SessionChanged += OnSessionChanged;
    }

    private void OnStateChanged() => InvokeAsync(StateHasChanged);
    private void OnSessionChanged() => InvokeAsync(StateHasChanged);

    private async Task OnPlay() => await Remote.SendPlayAsync();
    private async Task OnPause() => await Remote.SendPauseAsync();
    private async Task OnStop() => await Remote.SendStopAsync();

    private async Task OnSeekChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out var position))
        {
            await Remote.SendSeekAsync(position);
        }
    }

    private async Task OnVolumeChanged(ChangeEventArgs e)
    {
        if (double.TryParse(e.Value?.ToString(), out var volume))
        {
            await Remote.SendVolumeAsync(volume);
        }
    }

    private async Task OnAudioTrackSelected(int trackIndex)
    {
        await Remote.SendAudioTrackAsync(trackIndex);
    }

    private async Task OnSubtitleTrackSelected(int trackIndex)
    {
        await Remote.SendSubtitleTrackAsync(trackIndex);
    }

    private async Task OnResumeHere()
    {
        await OnResumeRequested.InvokeAsync();
    }

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }

    public void Dispose()
    {
        Remote.StateChanged -= OnStateChanged;
        Remote.SessionChanged -= OnSessionChanged;
    }
}
