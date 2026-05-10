using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Diagnostics;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminDiagnosticsPanel
{
    [Inject] private IDiagnosticsService DiagnosticsService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "libraryId")]
    public Guid? QueryLibraryId { get; set; }

    private List<LibraryHealthSummaryDto>? _summaries;
    private PaginatedListDto<DiagnosticItemDto>? _items;
    private bool _isLoadingSummary = true;
    private bool _isLoadingItems;
    private bool _isBulkFixing;
    private int _pageNumber = 1;
    private string? _selectedSeverity;

    private Guid? _filterLibraryId;
    private DiagnosticEntityType? _filterEntityType;
    private DiagnosticIssue? _filterIssue;

    private HashSet<DiagnosticItemDto> _selectedItems = [];

    private int _totalIssueCount;
    private int _errorCount;
    private int _warningCount;
    private int _infoCount;

    private Dictionary<string, string> _activeFilterChips => GetActiveFilterChips();

    private bool HasActiveFilters => _filterLibraryId.HasValue || _filterEntityType.HasValue || _filterIssue.HasValue;

    protected override async Task OnInitializedAsync()
    {
        if (QueryLibraryId.HasValue)
        {
            _filterLibraryId = QueryLibraryId.Value;
        }

        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        _isLoadingSummary = true;
        try
        {
            _summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync();
            ComputeAggregateCounts();
        }
        catch
        {
            _summaries = null;
            Snackbar.Add(S["LoadError"], K7Severity.Error);
        }
        finally
        {
            _isLoadingSummary = false;
        }

        await LoadItemsAsync();
    }

    private void ComputeAggregateCounts()
    {
        if (_summaries is null) return;

        _errorCount = _summaries.Sum(l => l.OrphanIndexedFileCount + l.MediaWithoutFilesCount + l.MissingFileMetadataCount);
        _warningCount = _summaries.Sum(l => l.UnidentifiedIndexedFileCount + l.MissingHlsSegmentsCount
            + l.MediaMissingPicturesCount + l.MediaMissingMetadataCount + l.StaleMetadataCount + l.InaccessiblePathCount);
        _infoCount = _summaries.Sum(l => l.MissingAudioAnalysisCount);
        _totalIssueCount = _errorCount + _warningCount + _infoCount;
    }

    private async Task LoadItemsAsync()
    {
        _isLoadingItems = true;
        _selectedItems.Clear();
        try
        {
            var severityIssues = GetSeverityIssues(_selectedSeverity);
            _items = await DiagnosticsService.GetDiagnosticItemsAsync(
                _filterLibraryId, _filterEntityType, _filterIssue, severityIssues, _pageNumber);
        }
        catch
        {
            _items = null;
        }
        finally
        {
            _isLoadingItems = false;
        }
    }

    private static IReadOnlyCollection<DiagnosticIssue>? GetSeverityIssues(string? severity) => severity switch
    {
        "error" => [DiagnosticIssue.OrphanFile, DiagnosticIssue.MissingFiles, DiagnosticIssue.MissingFileMetadata],
        "warning" => [DiagnosticIssue.UnidentifiedFile, DiagnosticIssue.MissingHlsSegments, DiagnosticIssue.MissingPictures, DiagnosticIssue.MissingMetadata, DiagnosticIssue.StaleMetadata, DiagnosticIssue.InaccessiblePath],
        "info" => [DiagnosticIssue.MissingAudioAnalysis],
        _ => null
    };

    private async Task DrillDown(Guid libraryId, DiagnosticEntityType? entityType = null, DiagnosticIssue? issue = null)
    {
        _filterLibraryId = libraryId;
        _filterEntityType = entityType;
        _filterIssue = issue;
        _selectedSeverity = null;
        _pageNumber = 1;
        await LoadItemsAsync();
    }

    private async Task ClearFilters()
    {
        _filterLibraryId = null;
        _filterEntityType = null;
        _filterIssue = null;
        _selectedSeverity = null;
        _pageNumber = 1;
        await LoadItemsAsync();
    }

    private async Task RemoveFilter(string key)
    {
        switch (key)
        {
            case "library": _filterLibraryId = null; break;
            case "type": _filterEntityType = null; break;
            case "issue": _filterIssue = null; break;
        }
        _pageNumber = 1;
        await LoadItemsAsync();
    }

    private async Task OnSeverityFilterChanged(string? severity)
    {
        _selectedSeverity = severity;
        _filterIssue = null;
        _filterEntityType = null;
        _filterLibraryId = null;
        _pageNumber = 1;
        await LoadItemsAsync();
    }

    private async Task OnLibraryFilterChanged(Guid? libraryId)
    {
        _filterLibraryId = libraryId;
        _pageNumber = 1;
        await LoadItemsAsync();
    }

    private async Task OnEntityTypeFilterChanged(DiagnosticEntityType? entityType)
    {
        _filterEntityType = entityType;
        _pageNumber = 1;
        await LoadItemsAsync();
    }

    private async Task OnIssueFilterChanged(DiagnosticIssue? issue)
    {
        _filterIssue = issue;
        _pageNumber = 1;
        await LoadItemsAsync();
    }

    private async Task OnPageChanged(int page)
    {
        _pageNumber = page;
        await LoadItemsAsync();
    }

    private Task PreviousPage()
    {
        if (_pageNumber > 1)
            return OnPageChanged(_pageNumber - 1);
        return Task.CompletedTask;
    }

    private Task NextPage()
    {
        if (_pageNumber < (_items?.TotalPages ?? 1))
            return OnPageChanged(_pageNumber + 1);
        return Task.CompletedTask;
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

    private DiagnosticFixAction? GetBulkFixAction()
    {
        if (_filterIssue.HasValue)
        {
            return _filterIssue.Value switch
            {
                DiagnosticIssue.MissingPictures or DiagnosticIssue.MissingMetadata or DiagnosticIssue.StaleMetadata
                    or DiagnosticIssue.MissingAudioAnalysis
                    => DiagnosticFixAction.RefreshMetadata,
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

    private string SummaryCardClass(string? severity) =>
        $"cursor-pointer summary-card {(_selectedSeverity == severity ? "summary-card-active" : "")}";

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
        DiagnosticIssue.StaleMetadata => L["StaleMetadata"],
        DiagnosticIssue.MissingAudioAnalysis => L["MissingAudioAnalysis"],
        DiagnosticIssue.MissingFiles => L["MissingFiles"],
        DiagnosticIssue.InaccessiblePath => L["InaccessiblePath"],
        _ => issue.ToString()
    };

    private static string GetIssueColor(DiagnosticIssue issue) => issue switch
    {
        DiagnosticIssue.OrphanFile or DiagnosticIssue.MissingFiles or DiagnosticIssue.MissingFileMetadata
            => "error",
        DiagnosticIssue.StaleMetadata or DiagnosticIssue.MissingAudioAnalysis => "info",
        DiagnosticIssue.InaccessiblePath => "warning",
        _ => "warning"
    };
}
