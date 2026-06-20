using K7.Clients.Shared.Enums;
using K7.Clients.Shared.UI.Components;
using K7.Shared.Dtos;

namespace K7.Clients.Shared.UI.Pages.Stats;

public partial class PlaybackHistory
{
    private K7DataTable<PlaybackHistoryItemDto>? _tableRef;
    private string _selectedMediaType = "";
    private const int PageSize = 50;
    private int _tableKey;
    private List<ButtonGroupOption<string>> _mediaTypeOptions = [];

    protected override void OnInitialized()
    {
        _mediaTypeOptions =
        [
            new("", Label: L["All"]),
            new("3", Label: L["Music"]),
            new("1", Label: L["Movies"]),
            new("5", Label: L["TVShows"])
        ];
    }

    private async Task OnMediaTypeChanged(string mediaType)
    {
        _selectedMediaType = mediaType ?? "";
        _tableKey++;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<K7DataTableResult<PlaybackHistoryItemDto>> LoadServerDataAsync(
        K7DataTableState<PlaybackHistoryItemDto> state, CancellationToken cancellationToken)
    {
        var startIndex = state.StartIndex;
        var count = state.Count;
        if (count <= 0) return new K7DataTableResult<PlaybackHistoryItemDto>([], 0);

        var mediaTypeParam = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType;

        var firstPage = (startIndex / PageSize) + 1;
        var lastPage = ((startIndex + count - 1) / PageSize) + 1;

        try
        {
            var tasks = Enumerable.Range(firstPage, lastPage - firstPage + 1)
                .Select(page => K7ServerService.GetPlaybackHistoryAsync(page, PageSize, mediaTypeParam, cancellationToken));

            var results = await Task.WhenAll(tasks);

            var totalCount = 0;
            var allItems = new List<PlaybackHistoryItemDto>(count);
            foreach (var result in results)
            {
                if (result?.Items is { Count: > 0 })
                {
                    totalCount = result.TotalCount;
                    allItems.AddRange(result.Items);
                }
            }

            var offset = startIndex - (firstPage - 1) * PageSize;
            var items = allItems.Skip(offset).Take(count).ToList();

            return new K7DataTableResult<PlaybackHistoryItemDto>(items, totalCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new K7DataTableResult<PlaybackHistoryItemDto>([], 0);
        }
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
            : $"{ts.Minutes}m {ts.Seconds:D2}s";
    }
}
