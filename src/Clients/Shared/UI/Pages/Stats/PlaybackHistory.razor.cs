using K7.Shared.Dtos;

namespace K7.Clients.Shared.UI.Pages.Stats;

public partial class PlaybackHistory
{
    private PlaybackHistoryPageDto? _history;
    private bool _loading = true;
    private int _currentPage = 1;
    private string _selectedMediaType = "";

    protected override async Task OnInitializedAsync()
    {
        await FetchHistoryAsync();
    }

    private async Task OnMediaTypeChanged(string mediaType)
    {
        _selectedMediaType = mediaType ?? "";
        _currentPage = 1;
        await FetchHistoryAsync();
    }

    private async Task OnPageChanged(int page)
    {
        _currentPage = page;
        await FetchHistoryAsync();
    }

    private async Task FetchHistoryAsync()
    {
        _loading = true;
        StateHasChanged();

        try
        {
            var mediaTypeParam = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType;
            _history = await K7ServerService.GetPlaybackHistoryAsync(_currentPage, 25, mediaTypeParam);
        }
        catch
        {
            _history = null;
        }

        _loading = false;
        StateHasChanged();
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
            : $"{ts.Minutes}m {ts.Seconds:D2}s";
    }
}
