using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Players;

/// <summary>
/// Isolated clock so 1 Hz remote position ticks do not re-render the parent panel
/// (and close subtitle / audio menus).
/// </summary>
public partial class RemoteControlSeekClock : ComponentBase, IDisposable
{
    [Parameter] public EventCallback<double> OnSeekRequested { get; set; }
    [Parameter] public Uri? ThumbnailsUri { get; set; }
    [Parameter] public List<SeekBar.Chapter> Chapters { get; set; } = [];

    private bool _disposed;

    protected override void OnInitialized() =>
        Remote.StateChanged += OnStateChanged;

    private void OnStateChanged() => InvokeAsync(StateHasChanged);

    private Task ForwardSeekAsync(double position) => OnSeekRequested.InvokeAsync(position);

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(Math.Max(0, seconds));
        return ts.Hours > 0
            ? ts.ToString(@"h\:mm\:ss")
            : ts.ToString(@"m\:ss");
    }

    private double DisplayPosition =>
        Remote.Duration > 0 ? Math.Min(Remote.Position, Remote.Duration) : Math.Max(0, Remote.Position);

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Remote.StateChanged -= OnStateChanged;
    }
}
