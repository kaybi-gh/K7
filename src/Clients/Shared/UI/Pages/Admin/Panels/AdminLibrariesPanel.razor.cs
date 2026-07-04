using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Diagnostics;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminLibrariesPanel : IDisposable
{
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;
    [Inject] private IDiagnosticsService DiagnosticsService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private K7HubClient K7HubClient { get; set; } = default!;

    private bool _isLoading = true;
    private List<LibraryDto>? _libraries;
    private Dictionary<Guid, int> _libraryIssueCountMap = [];
    private Dictionary<Guid, LibraryStatisticsDto> _libraryStatsMap = [];
    private readonly Dictionary<Guid, LibraryScanProgressState> _scanProgressMap = new();

    private IList<LibraryDto> _libraryItems => _libraries ?? [];

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.LibraryScanProgress += OnLibraryScanProgress;
        K7HubClient.LibraryScanCompleted += OnLibraryScanCompleted;
        await LoadLibraries();
    }

    public void Dispose()
    {
        K7HubClient.LibraryScanProgress -= OnLibraryScanProgress;
        K7HubClient.LibraryScanCompleted -= OnLibraryScanCompleted;
    }

    private void OnLibraryScanProgress(Guid libraryId, int processed, int total, string phase)
    {
        _scanProgressMap[libraryId] = new LibraryScanProgressState(processed, total, phase);
        _ = InvokeAsync(StateHasChanged);
    }

    private void OnLibraryScanCompleted(Guid libraryId, int addedCount, int skippedCount, int inaccessiblePathCount)
    {
        _scanProgressMap.Remove(libraryId);
        _ = InvokeAsync(async () =>
        {
            await LoadStatistics();
            StateHasChanged();
        });
    }

    private LibraryScanProgressState? GetScanProgress(Guid libraryId) =>
        _scanProgressMap.GetValueOrDefault(libraryId);

    private static double GetScanProgressValue(LibraryScanProgressState progress) =>
        progress.Total <= 0 ? 0 : Math.Clamp((double)progress.Processed / progress.Total * 100d, 0d, 100d);

    private string FormatScanProgress(LibraryScanProgressState progress) =>
        progress.Total > 0
            ? string.Format(L["ScanProgressFormat"], progress.Processed, progress.Total, progress.Phase)
            : string.Format(L["ScanProgressIndeterminate"], progress.Phase);

    private sealed record LibraryScanProgressState(int Processed, int Total, string Phase);

    private async Task LoadLibraries()
    {
        _isLoading = true;
        try
        {
            _libraries = await K7ServerService.GetLibrariesAsync();
            await Task.WhenAll(LoadIssueCounts(), LoadStatistics());
        }
        catch
        {
            _libraries = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task LoadStatistics()
    {
        try
        {
            var stats = await K7ServerService.GetLibraryStatisticsAsync();
            _libraryStatsMap = stats.ToDictionary(s => s.LibraryId);
        }
        catch
        {
            _libraryStatsMap = [];
        }
    }

    private async Task LoadIssueCounts()
    {
        try
        {
            var summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync();
            _libraryIssueCountMap = summaries.ToDictionary(
                s => s.LibraryId,
                LibraryHealthSummaryCounts.SumLibraryIssues);
        }
        catch
        {
            _libraryIssueCountMap = [];
        }
    }

    private int GetIssueCount(Guid libraryId) =>
        _libraryIssueCountMap.GetValueOrDefault(libraryId);

    private LibraryStatisticsDto? GetStats(Guid libraryId) =>
        _libraryStatsMap.GetValueOrDefault(libraryId);

    private string GetMediaTypeCountLabel(MediaType type) => type switch
    {
        MediaType.Movie => L["StatMovies"],
        MediaType.Serie => L["StatSeries"],
        MediaType.SerieSeason => L["StatSeasons"],
        MediaType.SerieEpisode => L["StatEpisodes"],
        MediaType.MusicArtist => L["StatArtists"],
        MediaType.MusicAlbum => L["StatAlbums"],
        MediaType.MusicTrack => L["StatTracks"],
        _ => type.ToString()
    };

    private void NavigateToDiagnostics(Guid libraryId) =>
        NavigationManager.NavigateTo($"/admin/diagnostics?libraryId={libraryId}");

    private async Task OpenCreateDialog()
    {
        var options = new K7DialogOptions {
            MaxWidth = K7DialogMaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<CreateLibraryDialog>(L["NewLibraryTitle"], null, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadLibraries();
        }
    }

    private static string GetMediaTypeIcon(LibraryMediaType type) => type switch
    {
        LibraryMediaType.Movie => Phosphor.FilmStrip,
        LibraryMediaType.Serie => Phosphor.Television,
        LibraryMediaType.Music => Phosphor.MusicNote,
        _ => Phosphor.Folder
    };

    private string GetMediaTypeLabel(LibraryMediaType type) => type switch
    {
        LibraryMediaType.Movie => S["MediaTypeMovies"],
        LibraryMediaType.Serie => S["MediaTypeSeries"],
        LibraryMediaType.Music => S["MediaTypeMusic"],
        _ => type.ToString()
    };

    private async Task IndexLibrary(LibraryDto library)
    {
        try
        {
            _scanProgressMap[library.Id] = new LibraryScanProgressState(0, 0, "queued");
            StateHasChanged();
            await K7ServerService.IndexLibraryFilesAsync(library.Id);
            Snackbar.Add(string.Format(L["IndexStarted"], library.Title), K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OpenUsersDialog(LibraryDto library)
    {
        var parameters = new K7DialogParameters<AdminLibraryUsersDialog>
        {
            { x => x.LibraryId, library.Id }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AdminLibraryUsersDialog>(string.Format(L["AccessTitle"], library.Title), parameters, options);
    }

    private async Task OpenEditDialog(LibraryDto library)
    {
        var providers = await K7ServerService.GetMetadataProvidersAsync(library.MediaType);
        var allGroups = await K7ServerService.GetLibraryGroupsAsync();
        var compatibleGroups = allGroups.Where(g => g.MediaType == library.MediaType).ToList();

        var parameters = new K7DialogParameters<EditLibraryDialog>
        {
            { x => x.LibraryId, library.Id },
            { x => x.Title, library.Title },
            { x => x.MediaType, library.MediaType },
            { x => x.AvailableProviders, providers },
            { x => x.SelectedProvider, library.MetadataProviderName },
            { x => x.MetadataRefreshIntervalDays, library.MetadataRefreshIntervalDays },
            { x => x.MetadataLanguage, library.MetadataLanguage },
            { x => x.MetadataFallbackLanguage, library.MetadataFallbackLanguage },
            { x => x.IsFederated, library.PeerServerId is not null },
            { x => x.AvailableGroups, compatibleGroups },
            { x => x.SelectedGroupId, library.LibraryGroupId },
            { x => x.IntroDetectionEnabled, library.IntroDetectionEnabled },
            { x => x.SeekbarThumbnailGenerationEnabled, library.SeekbarThumbnailGenerationEnabled },
            { x => x.MusicAudioAnalysisEnabled, library.MusicAudioAnalysisEnabled },
            { x => x.TranscodingEnabled, library.TranscodingEnabled },
            { x => x.TransmuxingEnabled, library.TransmuxingEnabled },
            { x => x.RealtimeMonitorEnabled, library.RealtimeMonitorEnabled },
            { x => x.AutoScanIntervalHours, library.AutoScanIntervalHours }
        };

        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<EditLibraryDialog>(string.Format(L["EditTitle"], library.Title), parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadLibraries();
        }
    }

    private async Task DeleteLibrary(LibraryDto library)
    {
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteDialogTitle"],
            string.Format(L["DeleteDialogMessage"], library.Title),
            yesText: L["DeleteConfirm"],
            cancelText: S["Cancel"]);

        if (confirmed is not true)
            return;

        try
        {
            await K7ServerService.DeleteLibraryAsync(library.Id);
            Snackbar.Add(string.Format(L["DeleteSuccess"], library.Title), K7Severity.Success);
            await LoadLibraries();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }
}
