using K7.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Components;

public partial class SyncedLyricsDisplay : ComponentBase
{
    [Inject] private IJSRuntime Js { get; set; } = default!;

    [Parameter] public string? LyricsLrc { get; set; }
    [Parameter] public string? PlainTextLyrics { get; set; }
    [Parameter] public double CurrentTime { get; set; }
    [Parameter] public EventCallback<double> SeekRequested { get; set; }

    private List<LrcLine> _lines = [];
    private Dictionary<int, ElementReference> _lineRefs = [];
    private int _activeIndex = -1;
    private string? _previousLrc;

    protected override void OnParametersSet()
    {
        if (_previousLrc != LyricsLrc)
        {
            _previousLrc = LyricsLrc;
            _lines = LrcParser.Parse(LyricsLrc);
            _lineRefs = new Dictionary<int, ElementReference>(_lines.Count);
            _activeIndex = -1;
        }

        if (_lines.Count == 0) return;

        var newIndex = FindActiveIndex(CurrentTime);
        if (newIndex != _activeIndex)
        {
            _activeIndex = newIndex;
            ScrollToActive();
        }
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

    private async void ScrollToActive()
    {
        if (_activeIndex >= 0 && _lineRefs.TryGetValue(_activeIndex, out var el))
        {
            try
            {
                await Js.InvokeVoidAsync("K7.scrollIntoViewSmooth", el);
            }
            catch
            {
                // Component may be disposed
            }
        }
    }

    private async Task OnLineClick(int index)
    {
        if (index >= 0 && index < _lines.Count && SeekRequested.HasDelegate)
            await SeekRequested.InvokeAsync(_lines[index].Timestamp.TotalSeconds);
    }
}
