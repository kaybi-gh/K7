using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Pages.Admin.Components;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminPlaybackHistoryPanel
{
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "userId")]
    public Guid? QueryUserId { get; set; }

    private K7DataTable<PlaybackHistoryItemDto>? _tableRef;
    private List<UserDto> _users = [];
    private Guid? _selectedUserId;
    private string _selectedMediaType = "";
    private const int PageSize = 50;
    private int _tableKey;
    private PlaybackHistoryItemDto? _selectedItem;

    private List<ButtonGroupOption<string>> _mediaTypeOptions = [];

    protected override async Task OnInitializedAsync()
    {
        _mediaTypeOptions =
        [
            new("", Label: L["All"]),
            new("MusicTrack", Label: L["Music"]),
            new("Movie", Label: L["Movies"]),
            new("SerieEpisode", Label: L["TVShows"])
        ];

        try
        {
            _users = await UserAdminService.GetUsersAsync();
        }
        catch
        {
            _users = [];
        }

        ApplyQueryUserFilter();
    }

    protected override void OnParametersSet()
    {
        if (_users.Count > 0)
            ApplyQueryUserFilter();
    }

    private void ApplyQueryUserFilter()
    {
        var targetUserId = QueryUserId;
        if (targetUserId.HasValue && _users.Count > 0 && _users.All(u => u.Id != targetUserId.Value))
            targetUserId = null;

        if (_selectedUserId == targetUserId)
            return;

        _selectedUserId = targetUserId;
        _tableKey++;
    }

    private void OnUserChanged(Guid? userId) =>
        SyncUserQueryParam(userId);

    private void SyncUserQueryParam(Guid? userId)
    {
        var path = NavigationManager.ToAbsoluteUri(NavigationManager.Uri).GetLeftPart(UriPartial.Path);

        if (userId is null)
        {
            if (!QueryUserId.HasValue)
                return;

            NavigationManager.NavigateTo(path, replace: true);
            return;
        }

        if (QueryUserId == userId)
            return;

        NavigationManager.NavigateTo($"{path}?userId={userId}", replace: true);
    }

    private void OnMediaTypeChanged(string mediaType)
    {
        _selectedMediaType = mediaType ?? "";
        RefreshTableAsync();
    }

    private void OnRowClicked(PlaybackHistoryItemDto item)
    {
        _selectedItem = _selectedItem == item ? null : item;
    }

    private void CloseDetail()
    {
        _selectedItem = null;
    }

    private void RefreshTableAsync()
    {
        _tableKey++;
        StateHasChanged();
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
                .Select(page => K7ServerService.GetAdminPlaybackHistoryAsync(page, PageSize, mediaTypeParam, _selectedUserId, cancellationToken));

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

    private static string FormatTranscodeReason(string reason)
    {
        return reason
            .Replace("VideoCodecNotSupported", "Video codec not supported")
            .Replace("AudioCodecNotSupported", "Audio codec not supported")
            .Replace("ContainerNotSupported", "Container not supported")
            .Replace("HlsSegmentsUnavailable", "HLS segments unavailable")
            .Replace("SubtitlesBurnIn", "Subtitle burn-in")
            .Replace("ResolutionNotSupported", "Resolution not supported")
            .Replace(", ", " | ");
    }

    private StreamDetailModel BuildDetailModel(PlaybackHistoryItemDto item)
    {
        var sq = item.StreamQuality;
        var hasStream = sq is not null;
        var isTranscode = sq?.IsTranscode == true;

        string? modeLabel = null;
        string? modeBadgeVariant = null;
        if (hasStream)
        {
            if (isTranscode)
            {
                modeLabel = "Transcode";
                modeBadgeVariant = "transcode";
            }
            else if (sq!.VideoDecision == "Direct" && sq.AudioDecision == "Direct")
            {
                modeLabel = "Direct";
                modeBadgeVariant = "direct";
            }
            else
            {
                modeLabel = "Transmux";
                modeBadgeVariant = "transmux";
            }
        }

        return new StreamDetailModel
        {
            MediaTitle = item.MediaTitle,
            MediaType = item.MediaType,
            Status = item.IsCompleted ? L["Watched"] : L["Incomplete"],
            StatusVariant = item.IsCompleted ? "success" : "warning",
            StartedAt = item.StartedAt,
            StoppedAt = item.StoppedAt,
            DurationDisplay = FormatDuration(item.TotalWatchedSeconds),
            UserName = item.UserName,
            DeviceName = item.DeviceName,
            DeviceClient = item.DeviceClient,
            HasStreamDetails = hasStream,
            ModeLabel = modeLabel,
            ModeBadgeVariant = modeBadgeVariant,
            VideoDecision = sq?.VideoDecision,
            AudioDecision = sq?.AudioDecision,
            SourceVideoCodec = sq?.SourceVideoCodec,
            SourceAudioCodec = sq?.SourceAudioCodec,
            StreamVideoCodec = sq?.StreamVideoCodec,
            StreamAudioCodec = sq?.StreamAudioCodec,
            Resolution = sq?.SourceResolution,
            TranscodeReason = sq?.TranscodeReason is not null ? FormatTranscodeReason(sq.TranscodeReason) : null,
            Bitrate = sq?.Bitrate is > 0 ? FormatBitrate(sq.Bitrate.Value) : null,
            AudioTrackLanguage = sq?.AudioTrackLanguage,
            AudioTrackTitle = sq?.AudioTrackTitle,
            AudioChannelLayout = sq?.AudioChannelLayout,
            SubtitleTrackLanguage = sq?.SubtitleTrackLanguage,
            SubtitleTrackTitle = sq?.SubtitleTrackTitle
        };
    }

    private static string FormatBitrate(int bitrate)
    {
        return bitrate >= 1000
            ? $"{bitrate / 1000.0:0.#} Mbps"
            : $"{bitrate} Kbps";
    }
}
