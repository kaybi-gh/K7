using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminLibrariesPanel
{
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;

    private bool _isLoading = true;
    private List<LibraryDto>? _libraries;

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

    private async Task OpenCreateDialog()
    {
        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Medium,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<Dialogs.CreateLibraryDialog>(L["NewLibraryTitle"], options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadLibraries();
        }
    }

    private static string GetMediaTypeIcon(LibraryMediaType type) => type switch
    {
        LibraryMediaType.Movie => Icons.Material.Filled.Theaters,
        LibraryMediaType.Serie => Icons.Material.Filled.Tv,
        LibraryMediaType.Music => Icons.Material.Filled.MusicNote,
        _ => Icons.Material.Filled.Folder
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
            Snackbar.Add(string.Format(L["IndexStarted"], library.Title), Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
    }

    private async Task OpenUsersDialog(LibraryDto library)
    {
        var parameters = new DialogParameters<AdminLibraryUsersDialog>
        {
            { x => x.LibraryId, library.Id }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AdminLibraryUsersDialog>(string.Format(L["AccessTitle"], library.Title), parameters, options);
    }

    private async Task OpenEditDialog(LibraryDto library)
    {
        var providers = await K7ServerService.GetMetadataProvidersAsync(library.MediaType);

        var parameters = new DialogParameters<Dialogs.EditLibraryDialog>
        {
            { x => x.Title, library.Title },
            { x => x.AvailableProviders, providers },
            { x => x.SelectedProvider, library.MetadataProviderName }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<Dialogs.EditLibraryDialog>(string.Format(L["EditTitle"], library.Title), parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: UpdateLibraryRequest request })
        {
            try
            {
                await K7ServerService.UpdateLibraryAsync(library.Id, request);
                await LoadLibraries();
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
            }
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
            Snackbar.Add(string.Format(L["DeleteSuccess"], library.Title), Severity.Success);
            await LoadLibraries();
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
    }
}
