using K7.Clients.Shared.UI.Helpers;
using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

public partial class SyncedLyricsDisplay : ComponentBase, IDisposable
{
    private const int ResyncIdleMs = 3000;
    private static readonly TimeSpan ProgrammaticScrollIgnore = TimeSpan.FromMilliseconds(700);

    [Inject] private IJSRuntime Js { get; set; } = default!;
    [Inject] private IStringLocalizer<SyncedLyricsDisplay> L { get; set; } = default!;

    [Parameter] public string? LyricsLrc { get; set; }
    [Parameter] public string? PlainTextLyrics { get; set; }
    [Parameter] public double CurrentTime { get; set; }
    [Parameter] public EventCallback<double> SeekRequested { get; set; }

    private List<LrcLine> _lines = [];
    private Dictionary<int, ElementReference> _lineRefs = [];
    private ElementReference _scrollContainer;
    private int _activeIndex = -1;
    private string? _previousLrc;
    private bool _userBrowsing;
    private bool _pendingScroll;
    private bool _pendingFocus;
    private DateTime _ignoreScrollUntilUtc;
    private DebouncedActionRunner? _resyncRunner;

    protected override void OnInitialized()
    {
        _resyncRunner = new DebouncedActionRunner(ResyncAfterIdleAsync, InvokeAsync, ResyncIdleMs);
    }

    protected override void OnParametersSet()
    {
        if (_previousLrc != LyricsLrc)
        {
            _previousLrc = LyricsLrc;
            _lines = LrcParser.Parse(LyricsLrc);
            _lineRefs = new Dictionary<int, ElementReference>(_lines.Count);
            _activeIndex = -1;
            _userBrowsing = false;
        }

        if (_lines.Count == 0) return;

        var newIndex = FindActiveIndex(CurrentTime);
        if (newIndex == _activeIndex) return;

        _activeIndex = newIndex;
        if (!_userBrowsing)
            _pendingScroll = true;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_pendingScroll) return;
        if (_activeIndex < 0 || !_lineRefs.ContainsKey(_activeIndex)) return;

        _pendingScroll = false;
        var moveFocus = _pendingFocus;
        _pendingFocus = false;
        await ScrollToActiveAsync(moveFocus);
    }

    private int FindActiveIndex(double currentTimeSeconds)
    {
        var current = TimeSpan.FromSeconds(currentTimeSeconds);
        var index = -1;
        for (var i = 0; i < _lines.Count; i++)
        {
            if (_lines[i].Timestamp <= current)
                index = i;
            else
                break;
        }
        return index;
    }

    private void OnUserScroll()
    {
        if (DateTime.UtcNow < _ignoreScrollUntilUtc) return;

        PauseAutoScroll();
    }

    private void OnLineFocus(int index)
    {
        if (index == _activeIndex) return;
        PauseAutoScroll();
    }

    private void PauseAutoScroll()
    {
        _userBrowsing = true;
        _resyncRunner?.Schedule();
    }

    private async Task ResyncAfterIdleAsync()
    {
        _userBrowsing = false;
        await ScrollToActiveAsync(moveFocusIfContained: true);
    }

    private async Task ScrollToActiveAsync(bool moveFocusIfContained = false)
    {
        if (_userBrowsing) return;
        if (_activeIndex < 0 || !_lineRefs.TryGetValue(_activeIndex, out var el)) return;

        _ignoreScrollUntilUtc = DateTime.UtcNow.Add(ProgrammaticScrollIgnore);
        try
        {
            await Js.InvokeVoidAsync("K7.scrollIntoViewSmooth", el, _scrollContainer);
            if (moveFocusIfContained)
                await Js.InvokeVoidAsync("K7.focusIfContained", el, _scrollContainer);
        }
        catch
        {
            // Component may be disposed
        }
    }

    private async Task OnLineClick(int index)
    {
        if (index < 0 || index >= _lines.Count || !SeekRequested.HasDelegate) return;

        _userBrowsing = false;
        _activeIndex = index;
        await ScrollToActiveAsync();
        await SeekRequested.InvokeAsync(_lines[index].Timestamp.TotalSeconds);
    }

    private async Task OnLineKeyDown(KeyboardEventArgs e, int index)
    {
        if (e.Code is "Enter" or "Space")
            await OnLineClick(index);
    }

    public void Dispose()
    {
        _resyncRunner?.Dispose();
        _resyncRunner = null;
    }
}
