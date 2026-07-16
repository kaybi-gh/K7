using K7.Clients.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7DateRangePicker : IAsyncDisposable
{
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [Parameter] public DateOnly? From { get; set; }
    [Parameter] public EventCallback<DateOnly?> FromChanged { get; set; }
    [Parameter] public DateOnly? To { get; set; }
    [Parameter] public EventCallback<DateOnly?> ToChanged { get; set; }
    [Parameter] public EventCallback<(DateOnly? From, DateOnly? To)> RangeChanged { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string HintSelectEnd { get; set; } = "Select end date";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public Func<DateOnly?, DateOnly?, string>? FormatLabel { get; set; }

    private bool _open;
    private ElementReference _dropdown;
    private DotNetObjectReference<LayerCloseCallback>? _closeCallbackRef;
    private DateOnly _viewDate = DateOnly.FromDateTime(DateTime.Today);
    private DateOnly? _pendingStart;
    private DateOnly? _pendingEnd;
    private List<DateOnly> _calendarDays = [];

    private static readonly string[] _weekDays = ["Lu", "Ma", "Me", "Je", "Ve", "Sa", "Di"];

    private string DisplayLabel
    {
        get
        {
            if (FormatLabel is not null)
                return FormatLabel(From, To);

            if (From is not null && To is not null)
                return $"{From:dd/MM/yyyy} - {To:dd/MM/yyyy}";

            if (From is not null)
                return $"{From:dd/MM/yyyy} - ...";

            return Label;
        }
    }

    protected override void OnParametersSet()
    {
        if (From is not null && _viewDate == DateOnly.FromDateTime(DateTime.Today) && !_open)
            _viewDate = From.Value;

        BuildCalendar();
    }

    private void BuildCalendar()
    {
        _calendarDays = [];
        var firstOfMonth = new DateOnly(_viewDate.Year, _viewDate.Month, 1);
        var startDow = ((int)firstOfMonth.DayOfWeek + 6) % 7;
        var start = firstOfMonth.AddDays(-startDow);

        for (var i = 0; i < 42; i++)
            _calendarDays.Add(start.AddDays(i));
    }

    private bool IsSelected(DateOnly d) => d == From || d == To;
    private bool IsRangeStart(DateOnly d) => d == From && To is not null;
    private bool IsRangeEnd(DateOnly d) => d == To && From is not null;

    private bool IsInRange(DateOnly d)
    {
        if (From is null || To is null) return false;
        return d > From && d < To;
    }

    private async Task OnDayClick(DateOnly day)
    {
        if (_pendingStart is null || _pendingEnd is not null)
        {
            _pendingStart = day;
            _pendingEnd = null;
            return;
        }

        _pendingEnd = day;

        var from = _pendingStart.Value <= _pendingEnd.Value ? _pendingStart.Value : _pendingEnd.Value;
        var to = _pendingStart.Value <= _pendingEnd.Value ? _pendingEnd.Value : _pendingStart.Value;

        await FromChanged.InvokeAsync(from);
        await ToChanged.InvokeAsync(to);
        await RangeChanged.InvokeAsync((from, to));

        _pendingStart = null;
        _pendingEnd = null;

        await CloseAsync();
    }

    private void PreviousMonth()
    {
        _viewDate = _viewDate.AddMonths(-1);
        BuildCalendar();
    }

    private void NextMonth()
    {
        _viewDate = _viewDate.AddMonths(1);
        BuildCalendar();
    }

    private async Task Toggle()
    {
        if (_open)
            await CloseAsync();
        else
            await OpenAsync();
    }

    private async Task OpenAsync()
    {
        _pendingStart = null;
        _pendingEnd = null;

        if (From is not null)
            _viewDate = From.Value;

        BuildCalendar();
        _open = true;
        StateHasChanged();
        await Task.Yield();

        _closeCallbackRef?.Dispose();
        _closeCallbackRef = DotNetObjectReference.Create(new LayerCloseCallback(OnLayerClosed));
        try
        {
            await SpatialNav.PushLayerAsync(_dropdown, "popover", new SpatialNavLayerOptions
            {
                OnClose = _closeCallbackRef
            });
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
    }

    private async Task CloseAsync()
    {
        if (!_open) return;
        _open = false;
        try
        {
            await SpatialNav.PopLayerAsync(_dropdown);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
        }
        StateHasChanged();
    }

    private void OnLayerClosed()
    {
        if (!_open) return;
        _open = false;
        _pendingStart = null;
        _pendingEnd = null;
        StateHasChanged();
    }

    public async ValueTask DisposeAsync()
    {
        if (_open)
        {
            try { await SpatialNav.PopLayerAsync(_dropdown); }
            catch (Exception ex) when (ex is JSException or InvalidOperationException) { }
        }
        _closeCallbackRef?.Dispose();
    }
}
