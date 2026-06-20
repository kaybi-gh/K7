using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public enum MediaCardVariant { Poster, Cover, Backdrop }

public partial class MediaCard : IDisposable
{
    [Parameter] public bool Skeleton { get; set; }
    [Parameter] public MediaCardViewModel Model { get; set; } = default!;
    [Parameter] public MediaCardVariant Variant { get; set; } = MediaCardVariant.Poster;
    [Parameter] public string? Href { get; set; }
    [Parameter] public bool OverlayEnabled { get; set; }
    [Parameter] public bool ProgressEnabled { get; set; }
    [Parameter] public bool WatchedStatusEnabled { get; set; }
    [Parameter] public bool FooterVisible { get; set; }
    [Parameter] public bool ExcludeMenuEnabled { get; set; }
    [Parameter] public bool IsAdmin { get; set; }
    [Parameter] public EventCallback OnExcludeForSelf { get; set; }
    [Parameter] public EventCallback OnExcludeForOthers { get; set; }
    [Parameter] public EventCallback OnFocused { get; set; }

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private const int LongPressDelayMs = 600;
    private const double LongPressMoveThresholdSquared = 100;

    private bool _menuOpen;
    private bool _longPressTriggered;
    private bool _preventNextClick;
    private CancellationTokenSource? _longPressCts;
    private double _touchStartX;
    private double _touchStartY;

    private bool LongPressEnabled => OverlayEnabled || ExcludeMenuEnabled;

    private string GetRootClass()
    {
        var classes = new List<string>();
        if (_menuOpen)
            classes.Add("media-card--menu-open");
        if (OverlayEnabled && !ExcludeMenuEnabled)
            classes.Add("media-card--play-only");
        return string.Join(" ", classes);
    }

    private string VariantClass => Variant switch
    {
        MediaCardVariant.Cover => "media-card--cover",
        MediaCardVariant.Backdrop => "media-card--backdrop",
        _ => "media-card--poster"
    };

    private bool ProgressBarIsHidden() => Model.Progress < 1 || Model.Progress >= 100;

    private void OnMenuOpenChanged(bool open) => _menuOpen = open;

    private void OnContextMenu(MouseEventArgs e)
    {
        if (!LongPressEnabled)
            return;

        _longPressTriggered = true;
        _preventNextClick = true;
        _menuOpen = true;
    }

    private void OnTouchStart(TouchEventArgs e)
    {
        if (!LongPressEnabled || e.Touches.Length == 0)
            return;

        CancelLongPress();
        _longPressTriggered = false;
        _touchStartX = e.Touches[0].ClientX;
        _touchStartY = e.Touches[0].ClientY;
        _longPressCts = new CancellationTokenSource();
        _ = WaitForLongPressAsync(_longPressCts.Token);
    }

    private void OnTouchMove(TouchEventArgs e)
    {
        if (_longPressCts is null || e.Touches.Length == 0)
            return;

        var dx = e.Touches[0].ClientX - _touchStartX;
        var dy = e.Touches[0].ClientY - _touchStartY;
        if (dx * dx + dy * dy > LongPressMoveThresholdSquared)
            CancelLongPress();
    }

    private void OnTouchEnd(TouchEventArgs e)
    {
        if (_longPressTriggered)
            _preventNextClick = true;

        CancelLongPress();
    }

    private void OnTouchCancel(TouchEventArgs e) => CancelLongPress();

    private async Task WaitForLongPressAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(LongPressDelayMs, cancellationToken);
            _longPressTriggered = true;
            _menuOpen = true;
            await InvokeAsync(StateHasChanged);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void CancelLongPress()
    {
        _longPressCts?.Cancel();
        _longPressCts?.Dispose();
        _longPressCts = null;
    }

    private void OnLinkClick(MouseEventArgs e)
    {
        if (_preventNextClick)
            _preventNextClick = false;
    }

    public void Dispose() => CancelLongPress();

    private void OnPlay()
    {
        if (!string.IsNullOrEmpty(Href))
            NavigationManager.NavigateTo(Href);
    }

    private string PlaceholderIcon => Variant switch
    {
        MediaCardVariant.Cover => Phosphor.VinylRecord,
        _ => Phosphor.FilmSlate
    };
}
