using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminDiagnosticsPanel
{
    private const string FilterStorageKey = "admin.diagnostics";

    [Inject] private IDiagnosticsService DiagnosticsService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "libraryId")]
    public Guid? QueryLibraryId { get; set; }

    [SupplyParameterFromQuery(Name = "severity")]
    public string? QuerySeverity { get; set; }

    [SupplyParameterFromQuery(Name = "entityType")]
    public string? QueryEntityType { get; set; }

    [SupplyParameterFromQuery(Name = "issue")]
    public string? QueryIssue { get; set; }

    private List<LibraryHealthSummaryDto>? _summaries;
    private K7DataTable<DiagnosticItemDto>? _tableRef;
    private bool _isLoadingSummary = true;
    private bool _isBulkFixing;
    private bool _isQueueingAllFixes;
    private string? _selectedSeverity;
    private const int PageSize = 50;
    private int _tableKey;

    private Guid? _filterLibraryId;
    private DiagnosticEntityType? _filterEntityType;
    private DiagnosticIssue? _filterIssue;

    private HashSet<DiagnosticItemDto> _selectedItems = [];

    private int _totalIssueCount;
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;
    private int _totalCount;
    private bool _pendingQuerySync;

    private Dictionary<string, string> _activeFilterChips => GetActiveFilterChips();

    private bool HasActiveFilters =>
        _selectedSeverity is not null
        || _filterLibraryId.HasValue
        || _filterEntityType.HasValue
        || _filterIssue.HasValue;

    protected override async Task OnInitializedAsync()
    {
        if (PageFilterUrlSync.HasAnyQuery(Navigation, "severity", "libraryId", "entityType", "issue"))
        {
            ApplyFiltersFromQuery();
            await SaveFiltersToStorageAsync();
        }
        else if (await LoadPersistedFiltersAsync())
        {
            _pendingQuerySync = true;
        }

        await LoadAsync();
    }

    protected override void OnAfterRender(bool firstRender) =>
        PageFilterUrlSync.SyncAfterRender(Navigation, firstRender, ref _pendingQuerySync, BuildFilterQuery());

    private void ApplyFiltersFromQuery()
    {
        _selectedSeverity = QuerySeverity ?? PageFilterUrlSync.GetQueryValue(Navigation, "severity");
        _filterLibraryId = QueryLibraryId
            ?? (Guid.TryParse(PageFilterUrlSync.GetQueryValue(Navigation, "libraryId"), out var libraryId) ? libraryId : null);
        var entityTypeValue = QueryEntityType ?? PageFilterUrlSync.GetQueryValue(Navigation, "entityType");
        _filterEntityType = Enum.TryParse<DiagnosticEntityType>(entityTypeValue, ignoreCase: true, out var entityType) ? entityType : null;
        var issueValue = QueryIssue ?? PageFilterUrlSync.GetQueryValue(Navigation, "issue");
        _filterIssue = Enum.TryParse<DiagnosticIssue>(issueValue, ignoreCase: true, out var issue) ? issue : null;
    }

    private void SyncFiltersToQuery() =>
        PageFilterUrlSync.SetQuery(Navigation, BuildFilterQuery());

    private Dictionary<string, string?> BuildFilterQuery() => new()
    {
        ["severity"] = _selectedSeverity,
        ["libraryId"] = _filterLibraryId?.ToString(),
        ["entityType"] = _filterEntityType?.ToString(),
        ["issue"] = _filterIssue?.ToString()
    };

    private async Task<bool> LoadPersistedFiltersAsync()
    {
        try
        {
            var state = await PageFilterStorage.LoadAsync<DiagnosticsFilterState>(FilterStorageKey, CancellationToken.None);
            if (state is null)
            {
                return false;
            }

            _selectedSeverity = state.Severity;
            _filterLibraryId = state.LibraryId;
            _filterEntityType = state.EntityType;
            _filterIssue = state.Issue;
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
                new DiagnosticsFilterState(_selectedSeverity, _filterLibraryId, _filterEntityType, _filterIssue),
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

    private async Task ClearFiltersAsync()
    {
        _filterLibraryId = null;
        _filterEntityType = null;
        _filterIssue = null;
        _selectedSeverity = null;
        await PageFilterStorage.ClearAsync(FilterStorageKey);
        SyncFiltersToQuery();
        await LoadItemsAsync();
    }

    private string GetActiveFiltersLabel()
    {
        var parts = new List<string>();
        if (_selectedSeverity is not null)
        {
            parts.Add(_selectedSeverity switch
            {
                "error" => L["SeverityErrors"],
                "warning" => L["SeverityWarnings"],
                "info" => L["SeverityInfo"],
                _ => _selectedSeverity
            });
        }

        if (_filterLibraryId is { } libraryId && _summaries is not null)
        {
            var title = _summaries.FirstOrDefault(s => s.LibraryId == libraryId)?.LibraryTitle;
            if (title is not null)
            {
                parts.Add(title);
            }
        }

        if (_filterEntityType is { } entityType)
        {
            parts.Add(entityType switch
            {
                DiagnosticEntityType.Media => L["EntityTypeMedia"],
                DiagnosticEntityType.IndexedFile => L["EntityTypeIndexedFile"],
                DiagnosticEntityType.Library => L["EntityTypeLibrary"],
                _ => entityType.ToString()
            });
        }

        if (_filterIssue is { } issue)
        {
            parts.Add(GetIssueLabel(issue));
        }

        return string.Join(" · ", parts);
    }

    private string? GetSeverityFilterSummary() => _selectedSeverity switch
    {
        "error" => L["SeverityErrors"].Value,
        "warning" => L["SeverityWarnings"].Value,
        "info" => L["SeverityInfo"].Value,
        _ => null
    };

    private string? GetLibraryFilterSummary() =>
        _filterLibraryId is { } libraryId
            ? _summaries?.FirstOrDefault(s => s.LibraryId == libraryId)?.LibraryTitle
            : null;

    private string? GetEntityTypeFilterSummary() => _filterEntityType switch
    {
        DiagnosticEntityType.Media => L["EntityTypeMedia"].Value,
        DiagnosticEntityType.IndexedFile => L["EntityTypeIndexedFile"].Value,
        DiagnosticEntityType.Library => L["EntityTypeLibrary"].Value,
        _ => null
    };

    private string? GetIssueFilterSummary() =>
        _filterIssue is { } issue ? GetIssueLabel(issue) : null;

    private string GetLibraryMenuLabel()
    {
        var summary = GetLibraryFilterSummary();
        return summary is null ? L["FilterLibrary"].Value : $"{L["FilterLibrary"]}: {summary}";
    }

    private string GetEntityTypeMenuLabel()
    {
        var summary = GetEntityTypeFilterSummary();
        return summary is null ? L["FilterEntityType"].Value : $"{L["FilterEntityType"]}: {summary}";
    }

    private string GetIssueMenuLabel()
    {
        var summary = GetIssueFilterSummary();
        return summary is null ? L["FilterIssue"].Value : $"{L["FilterIssue"]}: {summary}";
    }

    private async Task LoadAsync()
    {
        var isFirstLoad = _summaries is null;
        if (isFirstLoad)
        {
            _isLoadingSummary = true;
        }

        try
        {
            _summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync();
            ComputeAggregateCounts();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _summaries = null;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isLoadingSummary = false;
        }

        await LoadItemsAsync();
    }

    private DiagnosticsFilterContext ActiveFilters => new(
        _filterLibraryId,
        _filterEntityType,
        _filterIssue,
        GetSeverityIssues(_selectedSeverity));

    private void ComputeAggregateCounts()
    {
        if (_summaries is null) return;

        var context = ActiveFilters;
        var excludeSeverity = DiagnosticsFilterExclusions.Severity;

        _errorCount = LibraryHealthSummaryCounts.SumSeverity(
            _summaries, LibraryHealthSummaryCounts.ErrorIssues, context, excludeSeverity);
        _warningCount = LibraryHealthSummaryCounts.SumSeverity(
            _summaries, LibraryHealthSummaryCounts.WarningIssues, context, excludeSeverity);
        _infoCount = LibraryHealthSummaryCounts.SumSeverity(
            _summaries, LibraryHealthSummaryCounts.InfoIssues, context, excludeSeverity);
        _totalIssueCount = _errorCount + _warningCount + _infoCount;
    }

    private void RefreshFilterCounts()
    {
        ComputeAggregateCounts();
    }

    private void OnColumnPickerClick() => _tableRef?.ToggleColumnPicker();

    private async Task LoadItemsAsync()
    {
        _selectedItems.Clear();
        _totalCount = 0;
        RefreshFilterCounts();
        _tableKey++;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<K7DataTableResult<DiagnosticItemDto>> LoadServerDataAsync(
        K7DataTableState<DiagnosticItemDto> state, CancellationToken cancellationToken)
    {
        var startIndex = state.StartIndex;
        var count = state.Count;
        if (count <= 0) return new K7DataTableResult<DiagnosticItemDto>([], 0);

        var severityIssues = GetSeverityIssues(_selectedSeverity);

        var firstPage = (startIndex / PageSize) + 1;
        var lastPage = ((startIndex + count - 1) / PageSize) + 1;

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var tasks = Enumerable.Range(firstPage, lastPage - firstPage + 1)
                .Select(page => DiagnosticsService.GetDiagnosticItemsAsync(
                    _filterLibraryId, _filterEntityType, _filterIssue, severityIssues, page, PageSize, cancellationToken));

            var results = await Task.WhenAll(tasks);

            var totalCount = 0;
            var allItems = new List<DiagnosticItemDto>(count);
            foreach (var result in results)
            {
                if (result is null)
                {
                    continue;
                }

                if (result.TotalCount is { } tc)
                {
                    totalCount = tc;
                }

                if (result.Items is { Count: > 0 })
                {
                    allItems.AddRange(result.Items);
                }
            }

            var offset = startIndex - (firstPage - 1) * PageSize;
            var items = allItems.Skip(offset).Take(count).ToList();
            _totalCount = totalCount;
            await InvokeAsync(StateHasChanged);

            return new K7DataTableResult<DiagnosticItemDto>(items, totalCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return new K7DataTableResult<DiagnosticItemDto>([], 0);
        }
    }

    private static IReadOnlyCollection<DiagnosticIssue>? GetSeverityIssues(string? severity) => severity switch
    {
        "error" => [DiagnosticIssue.OrphanFile, DiagnosticIssue.MissingFiles, DiagnosticIssue.MissingFileMetadata],
        "warning" => [DiagnosticIssue.UnidentifiedFile, DiagnosticIssue.MissingHlsSegments, DiagnosticIssue.MissingPictures, DiagnosticIssue.MissingMetadata, DiagnosticIssue.MissingExternalId, DiagnosticIssue.StaleMetadata, DiagnosticIssue.InaccessiblePath],
        "info" => [DiagnosticIssue.MissingAudioAnalysis],
        _ => null
    };

    private async Task DrillDown(Guid libraryId, DiagnosticEntityType? entityType = null, DiagnosticIssue? issue = null)
    {
        _filterLibraryId = libraryId;
        _filterEntityType = entityType;
        _filterIssue = issue;
        _selectedSeverity = null;
        await PersistAndReloadAsync();
    }

    private async Task ClearFilters()
    {
        await ClearFiltersAsync();
    }

    private async Task RemoveFilter(string key)
    {
        switch (key)
        {
            case "library": _filterLibraryId = null; break;
            case "type": _filterEntityType = null; break;
            case "issue": _filterIssue = null; break;
        }
        await PersistAndReloadAsync();
    }

    private async Task OnSeverityFilterChanged(string? severity)
    {
        _selectedSeverity = severity;
        await PersistAndReloadAsync();
    }

    private async Task OnLibraryFilterChanged(Guid? libraryId)
    {
        _filterLibraryId = libraryId;
        await PersistAndReloadAsync();
    }

    private async Task OnEntityTypeFilterChanged(DiagnosticEntityType? entityType)
    {
        _filterEntityType = entityType;
        await PersistAndReloadAsync();
    }

    private async Task OnIssueFilterChanged(DiagnosticIssue? issue)
    {
        _filterIssue = issue;
        await PersistAndReloadAsync();
    }

    private async Task PersistAndReloadAsync()
    {
        await PersistFiltersAsync();
        await LoadItemsAsync();
    }

    private async Task FixItemAsync(Guid entityId, DiagnosticFixAction action)
    {
        try
        {
            await DiagnosticsService.FixDiagnosticItemsAsync([entityId], action);
            Snackbar.Add(L["FixQueued"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task BulkFixAsync()
    {
        var action = GetBulkFixAction();
        if (action is null || _selectedItems.Count == 0) return;

        if (!await ConfirmBulkFixAsync(_selectedItems.Count, _filterIssue, action.Value))
            return;

        _isBulkFixing = true;
        try
        {
            var ids = _selectedItems.Select(i => i.EntityId).ToList();
            var result = await DiagnosticsService.FixDiagnosticItemsAsync(ids, action.Value);
            Snackbar.Add(string.Format(L["BulkFixQueued"], result), K7Severity.Success);
            _selectedItems.Clear();
            await LoadItemsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isBulkFixing = false;
        }
    }

    private async Task QueueAllFixesAsync()
    {
        if (_filterIssue is not { } issue || GetBulkFixAction() is not { } action)
            return;

        var count = _totalCount;
        if (count == 0)
            return;

        if (!await ConfirmBulkFixAsync(count, issue, action))
            return;

        _isQueueingAllFixes = true;
        try
        {
            var result = await DiagnosticsService.QueueDiagnosticFixesAsync(issue, _filterLibraryId);
            Snackbar.Add(string.Format(L["AllFixesQueued"], result), K7Severity.Success);
            await LoadAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isQueueingAllFixes = false;
        }
    }

    private async Task<bool> ConfirmBulkFixAsync(int count, DiagnosticIssue? issue, DiagnosticFixAction action)
    {
        var issueLabel = issue.HasValue ? GetIssueLabel(issue.Value) : L["AllIssues"];
        var actionLabel = GetFixActionLabel(action);
        var libraryLabel = _filterLibraryId.HasValue && _summaries is not null
            ? _summaries.FirstOrDefault(l => l.LibraryId == _filterLibraryId.Value)?.LibraryTitle ?? L["FilterLibrary"]
            : L["AllLibraries"];

        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["ConfirmBulkFixTitle"],
            string.Format(L["ConfirmBulkFixMessage"], count, issueLabel, actionLabel, libraryLabel),
            yesText: L["ConfirmBulkFixConfirm"],
            cancelText: S["Cancel"]);

        return confirmed is true;
    }

    private DiagnosticFixAction? GetBulkFixAction()
    {
        if (_filterIssue.HasValue)
        {
            return _filterIssue.Value switch
            {
                DiagnosticIssue.MissingExternalId => DiagnosticFixAction.AutoReidentifyMetadata,
                DiagnosticIssue.MissingPictures or DiagnosticIssue.MissingMetadata or DiagnosticIssue.StaleMetadata
                    or DiagnosticIssue.MissingMembers => DiagnosticFixAction.RefreshMetadata,
                DiagnosticIssue.MissingAudioAnalysis => DiagnosticFixAction.AnalyzeMusicTrackAudio,
                DiagnosticIssue.MissingFileMetadata => DiagnosticFixAction.ExtractFileMetadata,
                DiagnosticIssue.MissingHlsSegments => DiagnosticFixAction.ComputeHlsSegments,
                _ => null
            };
        }

        var entityTypes = _selectedItems.Select(i => i.EntityType).Distinct().ToList();
        if (entityTypes is [DiagnosticEntityType.Media])
        {
            return DiagnosticFixAction.RefreshMetadata;
        }

        return null;
    }

    private static DiagnosticFixAction? GetFixActionForIssue(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.MissingExternalId => DiagnosticFixAction.AutoReidentifyMetadata,
        DiagnosticIssue.MissingPictures or DiagnosticIssue.MissingMetadata or DiagnosticIssue.StaleMetadata
            or DiagnosticIssue.MissingMembers => DiagnosticFixAction.RefreshMetadata,
        DiagnosticIssue.MissingAudioAnalysis => DiagnosticFixAction.AnalyzeMusicTrackAudio,
        DiagnosticIssue.MissingFileMetadata => DiagnosticFixAction.ExtractFileMetadata,
        DiagnosticIssue.MissingHlsSegments => DiagnosticFixAction.ComputeHlsSegments,
        _ => null
    };

    private string GetFixActionLabel(DiagnosticFixAction action) => action switch
    {
        DiagnosticFixAction.AutoReidentifyMetadata => L["ActionReidentify"],
        DiagnosticFixAction.RefreshMetadata => L["ActionRefresh"],
        DiagnosticFixAction.AnalyzeMusicTrackAudio => L["ActionAnalyzeAudio"],
        DiagnosticFixAction.ExtractFileMetadata => L["ActionExtract"],
        DiagnosticFixAction.ComputeHlsSegments => L["ActionHls"],
        _ => action.ToString()
    };

    private static string GetFixActionIcon(DiagnosticFixAction action) => action switch
    {
        DiagnosticFixAction.AutoReidentifyMetadata => Phosphor.MagnifyingGlass,
        DiagnosticFixAction.RefreshMetadata => Phosphor.ArrowClockwise,
        DiagnosticFixAction.AnalyzeMusicTrackAudio => Phosphor.Waveform,
        DiagnosticFixAction.ExtractFileMetadata => Phosphor.Code,
        DiagnosticFixAction.ComputeHlsSegments => Phosphor.Rows,
        _ => Phosphor.Wrench
    };

    private string SummaryCardClass(string? severity) =>
        _selectedSeverity == severity ? "summary-card-active" : "";

    private int GetIssueCount(DiagnosticIssue issue) =>
        _summaries is null
            ? 0
            : LibraryHealthSummaryCounts.SumIssue(
                _summaries, issue, ActiveFilters, DiagnosticsFilterExclusions.Issue);

    private int GetEntityTypeCount(DiagnosticEntityType entityType) =>
        _summaries is null
            ? 0
            : LibraryHealthSummaryCounts.SumEntityType(
                _summaries, entityType, ActiveFilters, DiagnosticsFilterExclusions.EntityType);

    private int GetLibraryIssueCount(LibraryHealthSummaryDto library) =>
        LibraryHealthSummaryCounts.SumLibraryIssues(
            library, ActiveFilters, DiagnosticsFilterExclusions.Library);

    private static string FormatFilterLabel(string label, int count) => $"{label} ({count})";

    private Dictionary<string, string> GetActiveFilterChips()
    {
        var chips = new Dictionary<string, string>();
        if (_filterLibraryId.HasValue && _summaries is not null)
        {
            var lib = _summaries.FirstOrDefault(l => l.LibraryId == _filterLibraryId.Value);
            if (lib is not null) chips["library"] = lib.LibraryTitle;
        }
        if (_filterEntityType.HasValue)
        {
            chips["type"] = _filterEntityType.Value switch
            {
                DiagnosticEntityType.Media => L["EntityTypeMedia"],
                DiagnosticEntityType.Library => L["EntityTypeLibrary"],
                _ => L["EntityTypeIndexedFile"]
            };
        }
        if (_filterIssue.HasValue) chips["issue"] = GetIssueLabel(_filterIssue.Value);
        return chips;
    }

    private string GetIssueDetail(DiagnosticItemDto item, DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.MissingPictures when item.MissingPictureTypes is { Count: > 0 }
            => string.Format(L["DetailMissingPictures"], string.Join(", ", item.MissingPictureTypes)),
        DiagnosticIssue.StaleMetadata when item.LastMetadataRefreshedAt is not null
            => string.Format(L["DetailStaleRefreshed"],
                item.LastMetadataRefreshedAt.Value.LocalDateTime.ToString("d"),
                item.MetadataRefreshIntervalDays),
        DiagnosticIssue.StaleMetadata => L["DetailNeverRefreshed"],
        DiagnosticIssue.InaccessiblePath when item.DetailText is not null => item.DetailText,
        _ => GetIssueLabel(issue)
    };

    private string GetIssueLabel(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile => L["OrphanFiles"],
        DiagnosticIssue.UnidentifiedFile => L["UnidentifiedFiles"],
        DiagnosticIssue.MissingFileMetadata => L["MissingFileMetadata"],
        DiagnosticIssue.MissingHlsSegments => L["MissingHlsSegments"],
        DiagnosticIssue.MissingPictures => L["MissingPictures"],
        DiagnosticIssue.MissingMetadata => L["MissingMetadata"],
        DiagnosticIssue.MissingExternalId => L["MissingExternalId"],
        DiagnosticIssue.StaleMetadata => L["StaleMetadata"],
        DiagnosticIssue.MissingAudioAnalysis => L["MissingAudioAnalysis"],
        DiagnosticIssue.MissingFiles => L["MissingFiles"],
        DiagnosticIssue.InaccessiblePath => L["InaccessiblePath"],
        DiagnosticIssue.MissingMembers => L["MissingMembers"],
        _ => issue.ToString()
    };

    private static string GetIssueColor(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile or DiagnosticIssue.MissingFiles or DiagnosticIssue.MissingFileMetadata
            => "error",
        DiagnosticIssue.StaleMetadata or DiagnosticIssue.MissingAudioAnalysis or DiagnosticIssue.MissingMembers => "info",
        DiagnosticIssue.InaccessiblePath => "warning",
        _ => "warning"
    };
}
