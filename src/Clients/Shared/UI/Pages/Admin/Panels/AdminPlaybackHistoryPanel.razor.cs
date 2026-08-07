using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages.Admin.Components;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Users;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminPlaybackHistoryPanel
{
    private const string FilterStorageKey = "admin.playback-history";

    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "userId")]
    public Guid? QueryUserId { get; set; }

    [SupplyParameterFromQuery(Name = "mediaType")]
    public string? QueryMediaType { get; set; }

    [SupplyParameterFromQuery(Name = "period")]
    public string? QueryPeriod { get; set; }

    [SupplyParameterFromQuery(Name = "from")]
    public string? QueryFrom { get; set; }

    [SupplyParameterFromQuery(Name = "to")]
    public string? QueryTo { get; set; }

    private K7DataTable<PlaybackHistoryItemDto>? _tableRef;
    private List<UserDto> _users = [];
    private Guid? _selectedUserId;
    private string _selectedMediaType = "";
    private string _selectedPeriod = "all";
    private DateOnly _fromDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));
    private DateOnly _toDate = DateOnly.FromDateTime(DateTime.Now);
    private const int PageSize = 50;
    private int _tableKey;
    private int _totalCount;
    private PlaybackHistoryItemDto? _selectedItem;

    private List<ButtonGroupOption<string>> _mediaTypeOptions = [];
    private List<ButtonGroupOption<string>> _periodOptions = [];
    private bool _pendingQuerySync;

    protected override async Task OnInitializedAsync()
    {
        _periodOptions =
        [
            new("week", Label: L["WeekShort"]),
            new("month", Label: L["MonthShort"]),
            new("year", Label: L["YearShort"]),
            new("all", Label: L["AllTime"]),
            new("custom", Label: L["CustomShort"])
        ];

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

        if (PageFilterUrlSync.HasAnyQuery(NavigationManager, "userId", "mediaType", "period", "from", "to"))
        {
            ApplyFiltersFromQuery();
            await SaveFiltersToStorageAsync();
            _tableKey++;
        }
        else if (await LoadPersistedFiltersAsync())
        {
            _tableKey++;
            _pendingQuerySync = true;
        }
    }

    protected override void OnAfterRender(bool firstRender) =>
        PageFilterUrlSync.SyncAfterRender(NavigationManager, firstRender, ref _pendingQuerySync, BuildFilterQuery());

    protected override void OnParametersSet()
    {
        if (_users.Count == 0)
        {
            return;
        }

        if (!PageFilterUrlSync.HasAnyQuery(NavigationManager, "userId", "mediaType", "period", "from", "to"))
        {
            return;
        }

        var previousUserId = _selectedUserId;
        var previousMediaType = _selectedMediaType;
        var previousPeriod = _selectedPeriod;
        var previousFrom = _fromDate;
        var previousTo = _toDate;
        ApplyFiltersFromQuery();
        if (previousUserId != _selectedUserId
            || previousMediaType != _selectedMediaType
            || previousPeriod != _selectedPeriod
            || previousFrom != _fromDate
            || previousTo != _toDate)
        {
            _tableKey++;
        }
    }

    private void ApplyFiltersFromQuery()
    {
        var targetUserId = QueryUserId
            ?? (Guid.TryParse(PageFilterUrlSync.GetQueryValue(NavigationManager, "userId"), out var userId) ? userId : null);
        if (targetUserId.HasValue && _users.Count > 0 && _users.All(u => u.Id != targetUserId.Value))
        {
            targetUserId = null;
        }

        _selectedUserId = targetUserId;
        _selectedMediaType = QueryMediaType ?? PageFilterUrlSync.GetQueryValue(NavigationManager, "mediaType") ?? "";
        _selectedPeriod = QueryPeriod ?? PageFilterUrlSync.GetQueryValue(NavigationManager, "period") ?? "all";

        var from = QueryFrom ?? PageFilterUrlSync.GetQueryValue(NavigationManager, "from");
        var to = QueryTo ?? PageFilterUrlSync.GetQueryValue(NavigationManager, "to");
        if (DateOnly.TryParse(from, out var fromDate))
        {
            _fromDate = fromDate;
        }

        if (DateOnly.TryParse(to, out var toDate))
        {
            _toDate = toDate;
        }
    }

    private void SyncFiltersToQuery() =>
        PageFilterUrlSync.SetQuery(NavigationManager, BuildFilterQuery());

    private Dictionary<string, string?> BuildFilterQuery() => new()
    {
        ["userId"] = _selectedUserId?.ToString(),
        ["mediaType"] = string.IsNullOrEmpty(_selectedMediaType) ? null : _selectedMediaType,
        ["period"] = _selectedPeriod is "all" ? null : _selectedPeriod,
        ["from"] = _selectedPeriod == "custom" ? _fromDate.ToString("yyyy-MM-dd") : null,
        ["to"] = _selectedPeriod == "custom" ? _toDate.ToString("yyyy-MM-dd") : null
    };

    private async Task<bool> LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<AdminPlaybackHistoryFilterState>(FilterStorageKey, CancellationToken.None);
            if (state is null)
            {
                return false;
            }

            _selectedUserId = state.UserId;
            _selectedMediaType = state.MediaType ?? "";
            _selectedPeriod = string.IsNullOrWhiteSpace(state.Period) ? "all" : state.Period;
            if (DateOnly.TryParse(state.From, out var from))
            {
                _fromDate = from;
            }

            if (DateOnly.TryParse(state.To, out var to))
            {
                _toDate = to;
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private async Task SaveFiltersToStorageAsync()
    {
        try
        {
            await PageFilterStorage.SaveAsync(
                FilterStorageKey,
                new AdminPlaybackHistoryFilterState(
                    _selectedUserId,
                    _selectedMediaType,
                    _selectedPeriod,
                    _selectedPeriod == "custom" ? _fromDate.ToString("yyyy-MM-dd") : null,
                    _selectedPeriod == "custom" ? _toDate.ToString("yyyy-MM-dd") : null),
                CancellationToken.None);
        }
        catch
        {
            // Non-critical
        }
    }

    private async Task PersistFiltersAsync()
    {
        await SaveFiltersToStorageAsync();
        SyncFiltersToQuery();
    }

    private async Task OnUserChanged(Guid? userId)
    {
        _selectedUserId = userId;
        await PersistFiltersAsync();
        RefreshTableAsync();
    }

    private async Task OnPeriodChanged(string period)
    {
        _selectedPeriod = period ?? "all";
        await PersistFiltersAsync();
        if (_selectedPeriod != "custom")
        {
            RefreshTableAsync();
        }
    }

    private async Task OnDateRangeChanged((DateOnly? From, DateOnly? To) range)
    {
        if (range.From is not null) _fromDate = range.From.Value;
        if (range.To is not null) _toDate = range.To.Value;
        await PersistFiltersAsync();
        RefreshTableAsync();
    }

    private async Task OnMediaTypeChanged(string mediaType)
    {
        _selectedMediaType = mediaType ?? "";
        await PersistFiltersAsync();
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

    private void OnColumnPickerClick() => _tableRef?.ToggleColumnPicker();

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
        DateTime? from = _selectedPeriod == "custom" ? _fromDate.ToDateTime(TimeOnly.MinValue) : null;
        DateTime? to = _selectedPeriod == "custom" ? _toDate.ToDateTime(TimeOnly.MaxValue) : null;

        var firstPage = (startIndex / PageSize) + 1;
        var lastPage = ((startIndex + count - 1) / PageSize) + 1;

        try
        {
            var tasks = Enumerable.Range(firstPage, lastPage - firstPage + 1)
                .Select(page => K7ServerService.GetAdminPlaybackHistoryAsync(
                    page, PageSize, mediaTypeParam, _selectedUserId, _selectedPeriod, from, to, cancellationToken));

            var results = await Task.WhenAll(tasks);

            var totalCount = 0;
            var allItems = new List<PlaybackHistoryItemDto>(count);
            foreach (var result in results)
            {
                if (result is null)
                {
                    continue;
                }

                totalCount = Math.Max(totalCount, result.TotalCount);
                if (result.Items is { Count: > 0 })
                {
                    allItems.AddRange(result.Items);
                }
            }

            var offset = startIndex - (firstPage - 1) * PageSize;
            var items = allItems.Skip(offset).Take(count).ToList();
            _totalCount = totalCount;
            await InvokeAsync(StateHasChanged);

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

    private static string FormatSubtitleLabel(StreamQualityDto sq)
    {
        if (!string.IsNullOrWhiteSpace(sq.SubtitleTrackTitle))
            return sq.SubtitleTrackTitle;

        if (!string.IsNullOrWhiteSpace(sq.SubtitleTrackLanguage))
            return K7.Shared.SupportedLanguages.GetDisplayLabel(sq.SubtitleTrackLanguage);

        return "-";
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
                modeLabel = L["Transcode"];
                modeBadgeVariant = "transcode";
            }
            else if (sq!.VideoDecision == "Direct" && sq.AudioDecision == "Direct")
            {
                modeLabel = L["Direct"];
                modeBadgeVariant = "direct";
            }
            else
            {
                modeLabel = L["Transmux"];
                modeBadgeVariant = "transmux";
            }
        }

        return new StreamDetailModel
        {
            MediaTitle = item.MediaTitle,
            MediaType = item.MediaType,
            MediaTypeLabel = MediaTypeLabelHelper.Format(item.MediaType, S),
            MediaUrl = item.MediaUrl,
            Status = StatusLabel(item),
            StatusVariant = StatusBadgeVariant(item),
            StartedAt = item.StartedAt,
            StoppedAt = item.StoppedAt,
            DurationDisplay = FormatDuration(item.TotalWatchedSeconds),
            UserName = item.UserName,
            SharedProfileName = item.SharedProfileName,
            DeviceName = item.DeviceName,
            DeviceClient = FormatDeviceClient(item.DeviceClient),
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
            SubtitleTrackTitle = sq?.SubtitleTrackTitle,
            IsSubtitleBurnIn = sq?.TranscodeReason?.Contains("SubtitlesBurnIn", StringComparison.Ordinal) == true
        };
    }

    private string StatusLabel(PlaybackHistoryItemDto item) =>
        item.IsCompleted ? L["Watched"] : item.IsSkipped ? L["Skipped"] : L["Incomplete"];

    private static string StatusBadgeVariant(PlaybackHistoryItemDto item) =>
        item.IsCompleted ? "success" : item.IsSkipped ? "muted" : "warning";

    private string FormatDeviceLabel(string? deviceName, string? deviceClient)
    {
        if (string.IsNullOrWhiteSpace(deviceName))
            return FormatDeviceClient(deviceClient) ?? "-";

        var client = FormatDeviceClient(deviceClient);
        if (string.IsNullOrWhiteSpace(client)
            || deviceName.Contains(client, StringComparison.OrdinalIgnoreCase))
            return deviceName;

        return $"{deviceName} · {client}";
    }

    private string? FormatDeviceClient(string? client) => client switch
    {
        "External" => L["ClientExternal"],
        "Native" => L["ClientNative"],
        "Web" => L["ClientWeb"],
        _ => client
    };

    private static string FormatBitrate(int bitrate)
    {
        return bitrate >= 1000
            ? $"{bitrate / 1000.0:0.#} Mbps"
            : $"{bitrate} Kbps";
    }
}
