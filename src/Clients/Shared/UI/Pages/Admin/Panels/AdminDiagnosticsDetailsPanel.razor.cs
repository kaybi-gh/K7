using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Diagnostics;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminDiagnosticsDetailsPanel : IDisposable
{
    private const string FilterStorageKey = "admin.diagnostics.details";

    [Inject] private IDiagnosticsService DiagnosticsService { get; set; } = default!;
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

    [SupplyParameterFromQuery(Name = "workClass")]
    public string? QueryWorkClass { get; set; }

    private List<LibraryHealthSummaryDto>? _summaries;
    private K7DataTable<DiagnosticItemDto>? _tableRef;
    private bool _isLoadingSummary = true;
    private bool _tableLoaded;
    private bool _isBulkFixing;
    private bool _isQueueingAllFixes;
    private string? _selectedSeverity;
    private const int PageSize = 50;
    private int _tableKey;

    private Guid? _filterLibraryId;
    private DiagnosticEntityType? _filterEntityType;
    private DiagnosticIssue? _filterIssue;
    private DiagnosticWorkClass? _filterWorkClass;

    private readonly HashSet<DiagnosticItemDto> _selectedItems = [];
    private readonly CancellationTokenSource _cts = new();

    private int _totalIssueCount;
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;
    private int _totalCount;
    private bool _pendingQuerySync;
    private int _tableLoadGeneration;

    private bool HasActiveFilters =>
        _selectedSeverity is not null
        || _filterLibraryId.HasValue
        || _filterEntityType.HasValue
        || _filterIssue.HasValue
        || _filterWorkClass.HasValue;

    /// <summary>
    /// Problem occurrences for the severity band implied by the current severity filter
    /// (or all bands when no severity is selected). Distinct from <see cref="_totalCount"/>,
    /// which is the number of entity rows in the table (one row can list several problems).
    /// </summary>
    private int FilteredProblemCount => _selectedSeverity switch
    {
        "error" => _errorCount,
        "warning" => _warningCount,
        "info" => _infoCount,
        _ => _totalIssueCount
    };

    private string GetResultCountLabel()
    {
        // One table row = one entity; filter severity chips sum issue occurrences.
        // When a media has several warnings, those numbers diverge - show both.
        if (_tableLoaded && FilteredProblemCount != _totalCount)
            return string.Format(L["EntityAndProblemCount"], _totalCount, FilteredProblemCount);

        return string.Format(S["ItemCount"], _totalCount);
    }

    private string? GetTableHostClass()
    {
        if (!_tableLoaded)
            return "diagnostics-table-host--measuring";
        if (_totalCount == 0)
            return "diagnostics-table-host--hidden";
        return null;
    }

    protected override async Task OnInitializedAsync()
    {
        if (PageFilterUrlSync.HasAnyQuery(Navigation, "severity", "libraryId", "entityType", "issue", "workClass"))
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
        if (Enum.TryParse<DiagnosticIssue>(issueValue, ignoreCase: true, out var issue))
        {
            _filterIssue = DiagnosticIssueTaxonomy.Canonicalize(issue);
        }
        else
        {
            _filterIssue = null;
        }

        var workClassValue = QueryWorkClass ?? PageFilterUrlSync.GetQueryValue(Navigation, "workClass");
        _filterWorkClass = Enum.TryParse<DiagnosticWorkClass>(workClassValue, ignoreCase: true, out var workClass)
            ? workClass
            : null;
    }

    private void SyncFiltersToQuery() =>
        PageFilterUrlSync.SetQuery(Navigation, BuildFilterQuery());

    private Dictionary<string, string?> BuildFilterQuery() => new()
    {
        ["severity"] = _selectedSeverity,
        ["libraryId"] = _filterLibraryId?.ToString(),
        ["entityType"] = _filterEntityType?.ToString(),
        ["issue"] = _filterIssue?.ToString(),
        ["workClass"] = _filterWorkClass?.ToString()
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
            _filterIssue = state.Issue is { } issue
                ? DiagnosticIssueTaxonomy.Canonicalize(issue)
                : null;
            _filterWorkClass = state.WorkClass;
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
                new DiagnosticsFilterState(
                    _selectedSeverity,
                    _filterLibraryId,
                    _filterEntityType,
                    _filterIssue,
                    _filterWorkClass),
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
        _filterWorkClass = null;
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

        if (_filterWorkClass is { } workClass)
        {
            parts.Add(GetWorkClassLabel(workClass));
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
        _isLoadingSummary = true;
        _tableLoaded = false;
        _tableLoadGeneration++;
        await InvokeAsync(StateHasChanged);

        try
        {
            _summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync(_cts.Token);
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
        GetCombinedIssuesFilter());

    private void ComputeAggregateCounts()
    {
        if (_summaries is null) return;

        var context = new DiagnosticsFilterContext(
            _filterLibraryId,
            _filterEntityType,
            _filterIssue,
            null);
        var excludeSeverity = DiagnosticsFilterExclusions.Severity;

        _errorCount = LibraryHealthSummaryCounts.SumSeverity(
            _summaries, FilterByWorkClass(LibraryHealthSummaryCounts.ErrorIssues), context, excludeSeverity);
        _warningCount = LibraryHealthSummaryCounts.SumSeverity(
            _summaries, FilterByWorkClass(LibraryHealthSummaryCounts.WarningIssues), context, excludeSeverity);
        _infoCount = LibraryHealthSummaryCounts.SumSeverity(
            _summaries, FilterByWorkClass(LibraryHealthSummaryCounts.InfoIssues), context, excludeSeverity);
        _totalIssueCount = _errorCount + _warningCount + _infoCount;
    }

    private IReadOnlyCollection<DiagnosticIssue> FilterByWorkClass(IReadOnlyCollection<DiagnosticIssue> issues)
    {
        if (_filterWorkClass is not { } workClass)
            return issues;

        var allowed = DiagnosticIssueTaxonomy.IssuesForWorkClass(workClass);
        return issues.Where(allowed.Contains).ToArray();
    }

    private IReadOnlyCollection<DiagnosticIssue>? GetSeverityOnlyIssues() => _selectedSeverity switch
    {
        "error" or "warning" or "info" => DiagnosticIssueTaxonomy.IssuesForSeverityFilter(_selectedSeverity),
        _ => null
    };

    private IReadOnlyCollection<DiagnosticIssue>? GetCombinedIssuesFilter()
    {
        var severityIssues = GetSeverityOnlyIssues();
        IReadOnlyCollection<DiagnosticIssue>? workClassIssues = _filterWorkClass is { } workClass
            ? DiagnosticIssueTaxonomy.IssuesForWorkClass(workClass)
            : null;

        if (severityIssues is null && workClassIssues is null)
            return null;
        if (severityIssues is null)
            return workClassIssues;
        if (workClassIssues is null)
            return severityIssues;

        return severityIssues.Intersect(workClassIssues).ToArray();
    }

    private void OnColumnPickerClick() => _tableRef?.ToggleColumnPicker();

    private async Task LoadItemsAsync()
    {
        // Remount the table so Virtualize reloads with the new filters.
        // Clear _tableLoaded so we show a loader; keep the host laid out (not display:none)
        // so Virtualize can measure, but visually hidden until the first successful page lands.
        _selectedItems.Clear();
        _totalCount = 0;
        _tableLoaded = false;
        _tableLoadGeneration++;
        ComputeAggregateCounts();
        _tableKey++;
        await InvokeAsync(StateHasChanged);
    }

    private async Task<K7DataTableResult<DiagnosticItemDto>> LoadServerDataAsync(
        K7DataTableState<DiagnosticItemDto> state, CancellationToken cancellationToken)
    {
        var startIndex = state.StartIndex;
        var count = state.Count;
        if (count <= 0) return new K7DataTableResult<DiagnosticItemDto>([], 0);

        // Specific issue and severity/workClass both map to the same `issues` query param (OR on the server).
        // When an issue is selected, send only that issue so it is not diluted by the combined set.
        var combinedIssues = GetCombinedIssuesFilter();
        DiagnosticIssue? issueFilter = _filterIssue;
        IReadOnlyCollection<DiagnosticIssue>? issuesFilter = null;
        if (_filterIssue is { } selectedIssue)
        {
            var canonical = DiagnosticIssueTaxonomy.Canonicalize(selectedIssue);
            if (combinedIssues is { Count: > 0 } && !combinedIssues.Contains(canonical))
            {
                ScheduleTableLoaded(0, cancellationToken);
                return new K7DataTableResult<DiagnosticItemDto>([], 0);
            }

            issueFilter = canonical;
        }
        else
        {
            issuesFilter = combinedIssues;
        }

        var firstPage = (startIndex / PageSize) + 1;
        var lastPage = ((startIndex + count - 1) / PageSize) + 1;
        var severityFilter = _selectedSeverity switch
        {
            "error" => DiagnosticSeverity.Error,
            "warning" => DiagnosticSeverity.Warning,
            "info" => DiagnosticSeverity.Info,
            _ => (DiagnosticSeverity?)null
        };

        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var tasks = Enumerable.Range(firstPage, lastPage - firstPage + 1)
                .Select(page => DiagnosticsService.GetDiagnosticItemsAsync(
                    _filterLibraryId, _filterEntityType, issueFilter, issuesFilter, page, PageSize, severityFilter, cancellationToken));

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

            // Return items to Virtualize first; only then drop the loading overlay.
            // StateHasChanged inside ProvideItems cancels the request and flashes an empty table.
            ScheduleTableLoaded(totalCount, cancellationToken);

            return new K7DataTableResult<DiagnosticItemDto>(items, totalCount);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            ScheduleTableLoaded(0, CancellationToken.None);
            return new K7DataTableResult<DiagnosticItemDto>([], 0);
        }
    }

    private void ScheduleTableLoaded(int totalCount, CancellationToken cancellationToken)
    {
        _totalCount = totalCount;
        var generation = _tableLoadGeneration;
        _ = FinishTableLoadAsync(generation, cancellationToken);
    }

    private async Task FinishTableLoadAsync(int generation, CancellationToken cancellationToken)
    {
        try
        {
            // Let Virtualize commit the provider result before we re-render the parent.
            await Task.Yield();
            if (cancellationToken.IsCancellationRequested || generation != _tableLoadGeneration)
                return;

            _tableLoaded = true;
            await InvokeAsync(StateHasChanged);
        }
        catch (OperationCanceledException)
        {
            // Refresh / filter change superseded this page fetch.
        }
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
        _filterIssue = issue is { } value ? DiagnosticIssueTaxonomy.Canonicalize(value) : null;
        await PersistAndReloadAsync();
    }

    private async Task OnWorkClassFilterChanged(DiagnosticWorkClass? workClass)
    {
        _filterWorkClass = workClass;
        await PersistAndReloadAsync();
    }

    private async Task PersistAndReloadAsync()
    {
        await PersistFiltersAsync();
        await LoadItemsAsync();
    }

    private async Task OpenHelpAsync()
    {
        var options = new K7DialogOptions
        {
            MaxWidth = K7DialogMaxWidth.Large,
            FullWidth = true,
            CloseOnEscapeKey = true
        };
        await DialogService.ShowAsync<Dialogs.DiagnosticsHelpDialog>(L["HelpTitle"], null, options);
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
            string.Format(
                L["ConfirmBulkFixMessage"],
                count,
                issueLabel,
                actionLabel,
                libraryLabel,
                GetBulkFixDetail(issue)),
            yesText: L["ConfirmBulkFixConfirm"],
            cancelText: S["Cancel"]);

        return confirmed is true;
    }

    private string GetBulkFixDetail(DiagnosticIssue? issue)
    {
        if (issue is null)
            return L["ConfirmBulkFixDetail_Generic"];

        var key = $"ConfirmBulkFixDetail_{DiagnosticIssueTaxonomy.Canonicalize(issue.Value)}";
        var detail = L[key];
        return detail.ResourceNotFound ? L["ConfirmBulkFixDetail_Generic"] : detail.Value;
    }

    private DiagnosticFixAction? GetBulkFixAction()
    {
        if (_filterIssue.HasValue)
        {
            return GetFixActionForIssue(_filterIssue.Value);
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
        DiagnosticIssue.MissingChapters => DiagnosticFixAction.ExtractChapters,
        DiagnosticIssue.MissingThemeSong => DiagnosticFixAction.ExtractSerieThemeSong,
        DiagnosticIssue.MissingIntroOutro => DiagnosticFixAction.DetectMediaSegments,
        DiagnosticIssue.OrphanFile => DiagnosticFixAction.RetryCreateMedia,
        _ => null
    };

    private string GetFixActionLabel(DiagnosticFixAction action) => action switch
    {
        DiagnosticFixAction.AutoReidentifyMetadata => L["ActionReidentify"],
        DiagnosticFixAction.RefreshMetadata => L["ActionRefresh"],
        DiagnosticFixAction.AnalyzeMusicTrackAudio => L["ActionAnalyzeAudio"],
        DiagnosticFixAction.ExtractFileMetadata => L["ActionExtract"],
        DiagnosticFixAction.ComputeHlsSegments => L["ActionHls"],
        DiagnosticFixAction.ExtractChapters => L["ActionExtractChapters"],
        DiagnosticFixAction.ExtractSerieThemeSong => L["ActionExtractThemeSong"],
        DiagnosticFixAction.DetectMediaSegments => L["ActionDetectIntroOutro"],
        DiagnosticFixAction.RetryCreateMedia => L["ActionRetryCreateMedia"],
        _ => action.ToString()
    };

    private static string GetFixActionIcon(DiagnosticFixAction action) => action switch
    {
        DiagnosticFixAction.AutoReidentifyMetadata => Phosphor.MagnifyingGlass,
        DiagnosticFixAction.RefreshMetadata => Phosphor.ArrowClockwise,
        DiagnosticFixAction.AnalyzeMusicTrackAudio => Phosphor.Waveform,
        DiagnosticFixAction.ExtractFileMetadata => Phosphor.Code,
        DiagnosticFixAction.ComputeHlsSegments => Phosphor.Rows,
        DiagnosticFixAction.ExtractChapters => Phosphor.BookOpen,
        DiagnosticFixAction.ExtractSerieThemeSong => Phosphor.MusicNotes,
        DiagnosticFixAction.DetectMediaSegments => Phosphor.Waveform,
        DiagnosticFixAction.RetryCreateMedia => Phosphor.ArrowClockwise,
        _ => Phosphor.Wrench
    };

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

    private int GetWorkClassCount(DiagnosticWorkClass workClass)
    {
        if (_summaries is null)
            return 0;

        var context = new DiagnosticsFilterContext(
            _filterLibraryId,
            _filterEntityType,
            _filterIssue,
            GetSeverityOnlyIssues());

        return DiagnosticIssueTaxonomy.IssuesForWorkClass(workClass)
            .Sum(issue => LibraryHealthSummaryCounts.SumIssue(
                _summaries, issue, context, DiagnosticsFilterExclusions.None));
    }

    private static string FormatFilterLabel(string label, int count) => $"{label} ({count})";

    private string GetEntityKindLabel(DiagnosticItemDto item) => item.EntityType switch
    {
        DiagnosticEntityType.Library => L["EntityTypeLibrary"],
        DiagnosticEntityType.IndexedFile => L["EntityTypeIndexedFile"],
        DiagnosticEntityType.Media => item.MediaType switch
        {
            MediaType.Movie => L["MediaTypeMovie"],
            MediaType.Serie => L["MediaTypeSerie"],
            MediaType.SerieSeason => L["MediaTypeSerieSeason"],
            MediaType.SerieEpisode => L["MediaTypeSerieEpisode"],
            MediaType.MusicArtist => L["MediaTypeMusicArtist"],
            MediaType.MusicAlbum => L["MediaTypeMusicAlbum"],
            MediaType.MusicTrack => L["MediaTypeMusicTrack"],
            _ => L["EntityTypeMedia"]
        },
        _ => L["EntityTypeMedia"]
    };

    private string GetIssuesTooltip(DiagnosticItemDto item) =>
        string.Join(" · ", GetVisibleIssues(item).Select(issue => GetIssueDetail(item, issue)));

    private IEnumerable<DiagnosticIssue> GetVisibleIssues(DiagnosticItemDto item)
    {
        if (_selectedSeverity is null)
            return item.Issues;

        if (!Enum.TryParse<DiagnosticSeverity>(_selectedSeverity, ignoreCase: true, out var severityFilter))
            return item.Issues;

        return item.Issues.Where(issue =>
            DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssueTaxonomy.Canonicalize(issue)) == severityFilter);
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
        DiagnosticIssue.MissingChapters => L["MissingChapters"],
        DiagnosticIssue.MissingThemeSong => L["MissingThemeSong"],
        DiagnosticIssue.MissingIntroOutro => L["MissingIntroOutro"],
        DiagnosticIssue.MissingPictures => L["MissingPictures"],
        DiagnosticIssue.MissingMetadata => L["MissingMetadata"],
        DiagnosticIssue.MissingExternalId => L["MissingExternalId"],
        DiagnosticIssue.StaleMetadata => L["StaleMetadata"],
        DiagnosticIssue.MissingAudioAnalysis => L["MissingAudioAnalysis"],
        DiagnosticIssue.MissingFiles => L["MissingFiles"],
        DiagnosticIssue.InaccessiblePath => L["InaccessiblePath"],
        DiagnosticIssue.MissingMembers => L["MissingMembers"],
        DiagnosticIssue.DuplicateExternalId => L["DuplicateExternalId"],
        DiagnosticIssue.SuspectedDuplicateMedia => L["SuspectedDuplicateMedia"],
        _ => issue.ToString()
    };

    private string GetAppliesToLabel(DiagnosticIssue issue) =>
        DiagnosticIssueTaxonomy.GetEntityType(DiagnosticIssueTaxonomy.Canonicalize(issue)) switch
        {
            DiagnosticEntityType.IndexedFile => L["AppliesToIndexedFile"],
            DiagnosticEntityType.Library => L["AppliesToLibrary"],
            _ => L["AppliesToMedia"]
        };

    private string GetWorkClassLabel(DiagnosticWorkClass workClass) => workClass switch
    {
        DiagnosticWorkClass.Catalog => L["WorkClassCatalog"],
        DiagnosticWorkClass.Enrichment => L["WorkClassEnrichment"],
        DiagnosticWorkClass.Polish => L["WorkClassPolish"],
        _ => workClass.ToString()
    };

    private static string GetIssueColor(DiagnosticItemDto item, DiagnosticIssue issue)
    {
        var severity = DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssueTaxonomy.Canonicalize(issue));
        return severity switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Info => "info",
            _ => "warning"
        };
    }

    private static bool SupportsIndexedFileReIdentify(DiagnosticItemDto item, DiagnosticIssue issue) =>
        item.EntityType == DiagnosticEntityType.IndexedFile
        && DiagnosticIssueTaxonomy.Canonicalize(issue) is DiagnosticIssue.OrphanFile;

    private MediaType? GetReIdentifyMediaType(Guid libraryId) =>
        _summaries?.FirstOrDefault(s => s.LibraryId == libraryId)?.MediaType switch
        {
            LibraryMediaType.Movie => MediaType.Movie,
            LibraryMediaType.Serie => MediaType.Serie,
            LibraryMediaType.Music => MediaType.MusicAlbum,
            _ => null
        };

    private async Task OpenReIdentifyDialogAsync(DiagnosticItemDto item)
    {
        var mediaType = GetReIdentifyMediaType(item.LibraryId);
        if (mediaType is null)
            return;

        var (searchQuery, searchYear) = ReIdentifySearchDefaultsHelper.FromIdentification(
            item.Identification,
            mediaType.Value);
        searchQuery ??= item.EntityName;

        var parameters = new K7DialogParameters<ReIdentifyDialog>
        {
            { x => x.IndexedFileId, item.EntityId },
            { x => x.InitialSearchYear, searchYear },
            { x => x.MediaType, mediaType },
            { x => x.LibraryId, item.LibraryId },
            { x => x.SourcePath, item.DetailText }
        };

        if (mediaType == MediaType.MusicAlbum)
        {
            parameters.Add(x => x.InitialSearchArtist, item.Identification?.ArtistName);
            parameters.Add(x => x.InitialSearchAlbum, searchQuery);
        }
        else
        {
            parameters.Add(x => x.InitialSearchQuery, searchQuery);
        }

        var options = new K7DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = K7DialogMaxWidth.Medium,
            FullWidth = true
        };

        var dialog = await DialogService.ShowAsync<ReIdentifyDialog>(
            L["ReIdentifyIndexedFileDialogTitle"],
            parameters,
            options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(L["ReIdentifyIndexedFileSent"], K7Severity.Success);
            await LoadAsync();
        }
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
