using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Diagnostics;
using K7.Shared.Dtos.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminDiagnosticsPanel : IDisposable
{
    [Inject] private IDiagnosticsService DiagnosticsService { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "libraryId")]
    public Guid? QueryLibraryId { get; set; }

    private List<LibraryHealthSummaryDto>? _summaries;
    private List<LibraryHealthSummaryDto> _scopedSummaries = [];
    private List<LibraryHealthSummaryDto> _librariesWithIssues = [];
    private bool _isLoading = true;
    private bool _isQueueingFix;
    private bool _redirecting;
    private bool _isTv;
    private Guid? _filterLibraryId;
    private int _totalIssueCount;
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;
    private readonly CancellationTokenSource _cts = new();

    protected override void OnInitialized()
    {
        _isTv = DeviceService.CachedDeviceType == DeviceType.TV;
    }

    protected override async Task OnInitializedAsync()
    {
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;

        if (PageFilterUrlSync.HasAnyQuery(Navigation, "issue", "entityType", "severity", "workClass"))
        {
            _redirecting = true;
            var uri = Navigation.ToAbsoluteUri(Navigation.Uri);
            Navigation.NavigateTo($"/admin/diagnostics/details{uri.Query}", replace: true);
            return;
        }

        _filterLibraryId = QueryLibraryId
            ?? (Guid.TryParse(PageFilterUrlSync.GetQueryValue(Navigation, "libraryId"), out var libraryId)
                ? libraryId
                : null);

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (_redirecting)
            return;

        _isLoading = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            _summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync(_cts.Token);
            RefreshDerivedState();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _summaries = null;
            _scopedSummaries = [];
            _librariesWithIssues = [];
            _totalIssueCount = 0;
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void RefreshDerivedState()
    {
        if (_summaries is null)
        {
            _scopedSummaries = [];
            _librariesWithIssues = [];
            _totalIssueCount = 0;
            _errorCount = 0;
            _warningCount = 0;
            _infoCount = 0;
            return;
        }

        _scopedSummaries = _filterLibraryId is { } libraryId
            ? _summaries.Where(s => s.LibraryId == libraryId).ToList()
            : _summaries;

        _errorCount = LibraryHealthSummaryCounts.SumErrors(_scopedSummaries);
        _warningCount = LibraryHealthSummaryCounts.SumWarnings(_scopedSummaries);
        _infoCount = LibraryHealthSummaryCounts.SumInfo(_scopedSummaries);
        _totalIssueCount = _errorCount + _warningCount + _infoCount;

        _librariesWithIssues = _scopedSummaries
            .Where(s => LibraryHealthSummaryCounts.SumLibraryIssues(s) > 0)
            .OrderByDescending(s => LibraryHealthSummaryCounts.SumErrors([s]))
            .ThenByDescending(s => LibraryHealthSummaryCounts.SumWarnings([s]))
            .ThenByDescending(s => LibraryHealthSummaryCounts.SumInfo([s]))
            .ThenBy(s => s.LibraryTitle, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private int GetWorkClassCount(DiagnosticWorkClass workClass) =>
        LibraryHealthSummaryCounts.SumWorkClass(_scopedSummaries, workClass);

    private List<(DiagnosticIssue Issue, int Count)> GetIssuesWithCount(DiagnosticWorkClass workClass) =>
        DiagnosticIssueTaxonomy.IssuesForWorkClass(workClass)
            .Select(issue => (Issue: issue, Count: LibraryHealthSummaryCounts.SumIssue(_scopedSummaries, issue)))
            .Where(entry => entry.Count > 0)
            .OrderByDescending(entry => entry.Count)
            .ThenBy(entry => entry.Issue.ToString(), StringComparer.Ordinal)
            .ToList();

    private string BuildDetailsHref(
        string? severity = null,
        DiagnosticWorkClass? workClass = null,
        DiagnosticIssue? issue = null,
        Guid? libraryId = null)
    {
        var parameters = new Dictionary<string, object?>();
        var resolvedLibraryId = libraryId ?? _filterLibraryId;
        if (resolvedLibraryId is { } id)
            parameters["libraryId"] = id;
        if (severity is not null)
            parameters["severity"] = severity;
        if (workClass is { } wc)
            parameters["workClass"] = wc.ToString();
        if (issue is { } selectedIssue)
            parameters["issue"] = selectedIssue.ToString();

        return Navigation.GetUriWithQueryParameters("/admin/diagnostics/details", parameters);
    }

    private void NavigateToDetails(
        string? severity = null,
        DiagnosticWorkClass? workClass = null,
        DiagnosticIssue? issue = null,
        Guid? libraryId = null) =>
        Navigation.NavigateTo(BuildDetailsHref(severity, workClass, issue, libraryId));

    private void OnLibraryRowClick(TableRowClickEventArgs<LibraryHealthSummaryDto> args) =>
        NavigateToDetails(libraryId: args.Item.LibraryId);

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

    private async Task QueueIssueFixAsync(DiagnosticIssue issue, int count)
    {
        var action = GetFixActionForIssue(issue);
        if (action is null || count <= 0)
            return;

        if (!await ConfirmBulkFixAsync(count, issue, action.Value))
            return;

        _isQueueingFix = true;
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
            _isQueueingFix = false;
        }
    }

    private async Task<bool> ConfirmBulkFixAsync(int count, DiagnosticIssue issue, DiagnosticFixAction action)
    {
        var issueLabel = GetIssueLabel(issue);
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

    private string GetBulkFixDetail(DiagnosticIssue issue)
    {
        var key = $"ConfirmBulkFixDetail_{DiagnosticIssueTaxonomy.Canonicalize(issue)}";
        var detail = L[key];
        return detail.ResourceNotFound ? L["ConfirmBulkFixDetail_Generic"] : detail.Value;
    }

    private static DiagnosticFixAction? GetFixActionForIssue(DiagnosticIssue issue) =>
        DiagnosticIssueTaxonomy.Canonicalize(issue) switch
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
        DiagnosticFixAction.ExtractFileMetadata => L["ActionExtract"],
        DiagnosticFixAction.ComputeHlsSegments => L["ActionHls"],
        DiagnosticFixAction.ExtractChapters => L["ActionExtractChapters"],
        DiagnosticFixAction.ExtractSerieThemeSong => L["ActionExtractThemeSong"],
        DiagnosticFixAction.DetectMediaSegments => L["ActionDetectIntroOutro"],
        DiagnosticFixAction.AnalyzeMusicTrackAudio => L["ActionAnalyzeAudio"],
        DiagnosticFixAction.RetryCreateMedia => L["ActionRetryCreateMedia"],
        _ => action.ToString()
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

    private string GetIssueCardCaption(DiagnosticIssue issue) => GetAppliesToLabel(issue);

    private string GetIssueLabelStyle(DiagnosticIssue issue) =>
        $"color: var(--color-{GetIssueColor(issue)})";

    private string GetWorkClassLabel(DiagnosticWorkClass workClass) => workClass switch
    {
        DiagnosticWorkClass.Catalog => L["WorkClassCatalog"],
        DiagnosticWorkClass.Enrichment => L["WorkClassEnrichment"],
        DiagnosticWorkClass.Polish => L["WorkClassPolish"],
        _ => workClass.ToString()
    };

    private static string GetIssueColor(DiagnosticIssue issue) =>
        DiagnosticIssueTaxonomy.GetSeverity(DiagnosticIssueTaxonomy.Canonicalize(issue)) switch
        {
            DiagnosticSeverity.Error => "error",
            DiagnosticSeverity.Info => "info",
            _ => "warning"
        };

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }
}
