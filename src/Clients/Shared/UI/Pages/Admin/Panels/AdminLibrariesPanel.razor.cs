using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Diagnostics;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminLibrariesPanel
{
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;
    [Inject] private IDiagnosticsService DiagnosticsService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private bool _isLoading = true;
    private List<LibraryDto>? _libraries;
    private Dictionary<Guid, int> _libraryIssueCountMap = [];

    private IList<LibraryDto> _libraryItems => _libraries ?? [];

    protected override async Task OnInitializedAsync()
    {
        await LoadLibraries();
    }

    private async Task LoadLibraries()
    {
        _isLoading = true;
        try
        {
            _libraries = await K7ServerService.GetLibrariesAsync();
            await LoadIssueCounts();
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

    private async Task LoadIssueCounts()
    {
        try
        {
            var summaries = await DiagnosticsService.GetDiagnosticsSummaryAsync();
            _libraryIssueCountMap = summaries.ToDictionary(
                s => s.LibraryId,
                s => s.OrphanIndexedFileCount + s.UnidentifiedIndexedFileCount + s.MissingFileMetadataCount
                    + s.MissingHlsSegmentsCount + s.MediaMissingPicturesCount + s.MediaMissingMetadataCount
                    + s.MediaWithoutFilesCount + s.StaleMetadataCount + s.MissingAudioAnalysisCount
                    + s.InaccessiblePathCount);
        }
        catch
        {
            _libraryIssueCountMap = [];
        }
    }

    private int GetIssueCount(Guid libraryId) =>
        _libraryIssueCountMap.GetValueOrDefault(libraryId);

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
        LibraryMediaType.Movie => "film-strip",
        LibraryMediaType.Serie => "television",
        LibraryMediaType.Music => "music-note",
        _ => "folder"
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

        var parameters = new K7DialogParameters<EditLibraryDialog>
        {
            { x => x.LibraryId, library.Id },
            { x => x.Title, library.Title },
            { x => x.Description, library.Description },
            { x => x.Icon, library.Icon },
            { x => x.CoverPictureId, library.CoverPictureId },
            { x => x.AvailableProviders, providers },
            { x => x.SelectedProvider, library.MetadataProviderName },
            { x => x.MetadataRefreshIntervalDays, library.MetadataRefreshIntervalDays }
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
