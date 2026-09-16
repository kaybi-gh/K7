using System.Globalization;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceHitParadePage : IDisposable
{
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IAudioPlayerService Audio { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "period")]
    public string? QueryPeriod { get; set; }

    [SupplyParameterFromQuery(Name = "scope")]
    public string? QueryScope { get; set; }

    [SupplyParameterFromQuery(Name = "year")]
    public string? QueryYear { get; set; }

    [SupplyParameterFromQuery(Name = "month")]
    public string? QueryMonth { get; set; }

    [SupplyParameterFromQuery(Name = "season")]
    public string? QuerySeason { get; set; }

    [SupplyParameterFromQuery(Name = "from")]
    public string? QueryFrom { get; set; }

    [SupplyParameterFromQuery(Name = "to")]
    public string? QueryTo { get; set; }

    private List<ButtonGroupOption<string>> _periodOptions = [];
    private List<ButtonGroupOption<string>> _scopeOptions = [];
    private List<TrackRow> _tracks = [];
    private string _period = MusicHitParadePeriods.All;
    private string _scope = MusicHitParadeScopes.Personal;
    private string _season = MusicHitParadeSeasons.Winter;
    private int _year = DateTime.Now.Year;
    private int _month = DateTime.Now.Month;
    private DateOnly _fromDate = DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));
    private DateOnly _toDate = DateOnly.FromDateTime(DateTime.Now);
    private bool _loading = true;
    private bool _isTv;
    private bool _pendingQuerySync;
    private int _loadVersion;

    private DateOnly _today = DateOnly.FromDateTime(DateTime.Now);

    private bool ShowPeriodNav =>
        _period is MusicHitParadePeriods.Year or MusicHitParadePeriods.Season or MusicHitParadePeriods.Month;

    private bool CanGoNext =>
        ShowPeriodNav && MusicHitParadeCalendar.HasNextPeriod(
            _period, _year, _month, _season, DateTimeOffset.Now, TimeZoneInfo.Local);

    private MusicHitParadeWindow? CurrentWindow => _period == MusicHitParadePeriods.Custom
        ? MusicHitParadeCalendar.GetCustomWindow(_fromDate, _toDate, TimeZoneInfo.Local)
        : MusicHitParadeCalendar.GetWindow(_period, _year, _month, _season, TimeZoneInfo.Local);

    private string PeriodLabel
    {
        get
        {
            if (_period == MusicHitParadePeriods.All)
                return L["PeriodAll"];

            if (_period == MusicHitParadePeriods.Year)
                return _year.ToString(CultureInfo.InvariantCulture);

            if (_period == MusicHitParadePeriods.Season)
                return $"{SeasonLabel(_season)} {_year}";

            var local = CurrentWindow?.From.LocalDateTime ?? new DateTime(_year, Math.Clamp(_month, 1, 12), 1);
            var raw = local.ToString("MMMM yyyy", CultureInfo.CurrentUICulture);
            return string.IsNullOrEmpty(raw)
                ? raw
                : char.ToUpper(raw[0], CultureInfo.CurrentUICulture) + raw[1..];
        }
    }

    protected override void OnInitialized()
    {
        _periodOptions =
        [
            new(MusicHitParadePeriods.All, Label: L["PeriodAll"]),
            new(MusicHitParadePeriods.Year, Label: L["PeriodYear"]),
            new(MusicHitParadePeriods.Season, Label: L["PeriodSeason"]),
            new(MusicHitParadePeriods.Month, Label: L["PeriodMonth"]),
            new(MusicHitParadePeriods.Custom, Label: L["PeriodCustom"])
        ];

        _scopeOptions =
        [
            new(MusicHitParadeScopes.Personal, Label: L["ScopePersonal"]),
            new(MusicHitParadeScopes.Server, Label: L["ScopeServer"])
        ];
    }

    protected override async Task OnInitializedAsync()
    {
        if (!await HasMusicLibraryAsync())
        {
            Navigation.NavigateTo("/my-space", replace: true);
            return;
        }

        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
        Audio.CurrentTrackChanged += OnCurrentTrackChanged;
        ApplyAnchor(DateTime.Now);

        if (PageFilterUrlSync.HasAnyQuery(Navigation, "period", "scope", "year", "month", "season", "from", "to"))
        {
            ApplyFiltersFromQuery();
        }
        else
        {
            _pendingQuerySync = true;
        }

        await LoadAsync();
    }

    protected override void OnAfterRender(bool firstRender) =>
        PageFilterUrlSync.SyncAfterRender(Navigation, firstRender, ref _pendingQuerySync, BuildFilterQuery());

    private void ApplyFiltersFromQuery()
    {
        _period = MusicHitParadeCalendar.NormalizePeriod(QueryPeriod ?? PageFilterUrlSync.GetQueryValue(Navigation, "period"));
        _scope = MusicHitParadeCalendar.NormalizeScope(QueryScope ?? PageFilterUrlSync.GetQueryValue(Navigation, "scope"));
        _season = MusicHitParadeCalendar.NormalizeSeason(QuerySeason ?? PageFilterUrlSync.GetQueryValue(Navigation, "season"));

        if (int.TryParse(QueryYear ?? PageFilterUrlSync.GetQueryValue(Navigation, "year"), out var year))
            _year = year;

        if (int.TryParse(QueryMonth ?? PageFilterUrlSync.GetQueryValue(Navigation, "month"), out var month))
            _month = Math.Clamp(month, 1, 12);

        var from = QueryFrom ?? PageFilterUrlSync.GetQueryValue(Navigation, "from");
        var to = QueryTo ?? PageFilterUrlSync.GetQueryValue(Navigation, "to");
        if (DateOnly.TryParse(from, out var fromDate))
            _fromDate = fromDate;
        if (DateOnly.TryParse(to, out var toDate))
            _toDate = toDate;

        if (ShowPeriodNav && MusicHitParadeCalendar.IsFuturePeriod(
                _period, _year, _month, _season, DateTimeOffset.Now, TimeZoneInfo.Local))
            ApplyAnchor(DateTime.Now);

        ClampCustomRange();
    }

    private Dictionary<string, string?> BuildFilterQuery() => new()
    {
        ["period"] = _period is MusicHitParadePeriods.All ? null : _period,
        ["scope"] = _scope is MusicHitParadeScopes.Personal ? null : _scope,
        ["year"] = ShowPeriodNav ? _year.ToString(CultureInfo.InvariantCulture) : null,
        ["month"] = _period is MusicHitParadePeriods.Month ? _month.ToString(CultureInfo.InvariantCulture) : null,
        ["season"] = _period is MusicHitParadePeriods.Season ? _season : null,
        ["from"] = _period is MusicHitParadePeriods.Custom ? _fromDate.ToString("yyyy-MM-dd") : null,
        ["to"] = _period is MusicHitParadePeriods.Custom ? _toDate.ToString("yyyy-MM-dd") : null
    };

    private async Task OnPeriodChanged(string period)
    {
        var next = MusicHitParadeCalendar.NormalizePeriod(period);
        if (_period == next)
            return;

        _period = next;
        if (_period == MusicHitParadePeriods.Custom)
            ResetCustomRange();
        else if (ShowPeriodNav)
            ApplyAnchor(DateTime.Now);

        SyncFiltersToQuery();
        await LoadAsync();
    }

    private async Task OnDateRangeChanged((DateOnly? From, DateOnly? To) range)
    {
        if (range.From is not null)
            _fromDate = range.From.Value;
        if (range.To is not null)
            _toDate = range.To.Value;

        ClampCustomRange();
        SyncFiltersToQuery();
        await LoadAsync();
    }

    private async Task OnScopeChanged(string scope)
    {
        var next = MusicHitParadeCalendar.NormalizeScope(scope);
        if (_scope == next)
            return;

        _scope = next;
        SyncFiltersToQuery();
        await LoadAsync();
    }

    private Task GoPreviousAsync() => ShiftPeriodAsync(-1);

    private Task GoNextAsync() => ShiftPeriodAsync(1);

    private async Task ShiftPeriodAsync(int delta)
    {
        if (_period == MusicHitParadePeriods.All || _period == MusicHitParadePeriods.Custom)
            return;

        if (delta > 0 && !CanGoNext)
            return;

        if (_period == MusicHitParadePeriods.Year)
        {
            _year += delta;
        }
        else if (_period == MusicHitParadePeriods.Month)
        {
            (_year, _month) = MusicHitParadeCalendar.ShiftMonth(_year, _month, delta);
        }
        else
        {
            (_year, _season) = MusicHitParadeCalendar.ShiftSeason(_year, _season, delta);
        }

        if (MusicHitParadeCalendar.IsFuturePeriod(
                _period, _year, _month, _season, DateTimeOffset.Now, TimeZoneInfo.Local))
            ApplyAnchor(DateTime.Now);

        SyncFiltersToQuery();
        await LoadAsync();
    }

    private async Task<bool> HasMusicLibraryAsync()
    {
        try
        {
            var libraries = await LibraryService.GetLibrariesAsync();
            return libraries.Any(l => l.MediaType == LibraryMediaType.Music);
        }
        catch
        {
            return false;
        }
    }

    private async Task LoadAsync()
    {
        var version = ++_loadVersion;
        _loading = true;
        var window = CurrentWindow;

        try
        {
            var result = await MediaService.GetMusicHitParadeAsync(
                period: _period,
                scope: _scope,
                count: 50,
                from: window?.From.UtcDateTime,
                to: window?.To.UtcDateTime,
                year: ShowPeriodNav ? _year : null,
                month: _period == MusicHitParadePeriods.Month ? _month : null,
                season: _period == MusicHitParadePeriods.Season ? _season : null);

            if (version != _loadVersion)
                return;

            _tracks = result?.Tracks
                .Select((item, index) => ToRow(item, index + 1))
                .ToList() ?? [];
        }
        catch
        {
            if (version != _loadVersion)
                return;

            _tracks = [];
        }

        _loading = false;
    }

    private void SyncFiltersToQuery() =>
        PageFilterUrlSync.SetQuery(Navigation, BuildFilterQuery());

    private void ResetCustomRange()
    {
        _toDate = _today;
        _fromDate = _toDate.AddMonths(-1);
    }

    private void ClampCustomRange()
    {
        if (_toDate > _today)
            _toDate = _today;
        if (_fromDate > _today)
            _fromDate = _today.AddMonths(-1);
        if (_fromDate > _toDate)
            _fromDate = _toDate;
    }

    private void ApplyAnchor(DateTime local)
    {
        _month = local.Month;
        if (_period == MusicHitParadePeriods.Season)
        {
            (_year, _season) = MusicHitParadeCalendar.SeasonFor(local);
            return;
        }

        _year = local.Year;
        _season = MusicHitParadeCalendar.SeasonFor(local).Season;
    }

    private void OnCurrentTrackChanged(AudioQueueItem? track)
    {
        var playingId = track?.MediaId;
        _tracks = _tracks
            .Select(t => t with { IsPlaying = t.Id == playingId })
            .ToList();
        _ = InvokeAsync(StateHasChanged);
    }

    public void Dispose() =>
        Audio.CurrentTrackChanged -= OnCurrentTrackChanged;

    private async Task OnTrackClick(TableRowClickEventArgs<TrackRow> args)
    {
        if (args.Item is null)
            return;

        await PlayTrackAsync(args.Item);
    }

    private async Task PlayTrackAsync(TrackRow track)
    {
        var queueItems = _tracks
            .Where(t => t.IndexedFileId.HasValue)
            .Select(BuildQueueItem)
            .ToList();

        if (queueItems.Count == 0)
            return;

        var index = queueItems.FindIndex(q => q.MediaId == track.Id);
        await Audio.PlayTracksAsync(queueItems, index >= 0 ? index : 0);
    }

    private MediaCardViewModel ToCardModel(TrackRow track) => new()
    {
        Id = track.Id.ToString(),
        Kind = MediaCardKind.Cover,
        Title = track.Title,
        PictureUrl = track.CoverUrl,
        AdditionalInformations = $"{track.PlayCount} {L["Plays"]}",
        UserRating = track.UserRating
    };

    private static string GetCardElementId(Guid id) => $"hit-parade-track-{id:N}";

    private TrackRow ToRow(PlayedMusicTrackDto item, int rank)
    {
        var track = item.Track;
        var picture = track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? track.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);
        var pictureSize = _isTv ? MetadataPictureSize.Medium : MetadataPictureSize.Small;

        return new TrackRow
        {
            Rank = rank,
            Id = track.Id,
            IndexedFileId = track.IndexedFileId,
            Title = track.Title ?? S["Untitled"],
            ArtistName = track.ArtistName,
            ArtistId = track.ArtistId,
            AlbumId = track.AlbumId,
            AlbumTitle = track.AlbumTitle,
            Duration = track.Duration ?? 0,
            PlayCount = item.PlayCount,
            CoverUrl = ApiClient.GetAbsoluteUri(picture?.GetUri(pictureSize)?.OriginalString)?.AbsoluteUri,
            CoverDominantColor = picture?.DominantColor,
            Genre = track.Genre,
            UserRating = track.UserRating,
            IsPlaying = Audio.CurrentTrack?.MediaId == track.Id
        };
    }

    private static AudioQueueItem BuildQueueItem(TrackRow track) => new()
    {
        IndexedFileId = track.IndexedFileId!.Value,
        MediaId = track.Id,
        Title = track.Title,
        Artist = track.ArtistName,
        ArtistId = track.ArtistId,
        AlbumTitle = track.AlbumTitle,
        Genre = track.Genre,
        CoverUrl = track.CoverUrl,
        CoverDominantColor = track.CoverDominantColor,
        Duration = track.Duration,
        UserRating = track.UserRating
    };

    private string SeasonLabel(string season) => MusicHitParadeCalendar.NormalizeSeason(season) switch
    {
        MusicHitParadeSeasons.Spring => L["SeasonSpring"],
        MusicHitParadeSeasons.Summer => L["SeasonSummer"],
        MusicHitParadeSeasons.Autumn => L["SeasonAutumn"],
        _ => L["SeasonWinter"]
    };

    private static string FormatTime(double seconds)
    {
        if (seconds <= 0)
            return "";

        var ts = TimeSpan.FromSeconds(seconds);
        return ts.Hours > 0
            ? $"{ts.Hours:0}:{ts.Minutes:00}:{ts.Seconds:00}"
            : $"{ts.Minutes:0}:{ts.Seconds:00}";
    }

    internal sealed record TrackRow
    {
        public int Rank { get; init; }
        public Guid Id { get; init; }
        public Guid? IndexedFileId { get; init; }
        public required string Title { get; init; }
        public string? ArtistName { get; init; }
        public Guid? ArtistId { get; init; }
        public Guid AlbumId { get; init; }
        public string? AlbumTitle { get; init; }
        public double Duration { get; init; }
        public int PlayCount { get; init; }
        public string? CoverUrl { get; init; }
        public string? CoverDominantColor { get; init; }
        public string? Genre { get; init; }
        public int? UserRating { get; init; }
        public bool IsPlaying { get; init; }
    }
}
