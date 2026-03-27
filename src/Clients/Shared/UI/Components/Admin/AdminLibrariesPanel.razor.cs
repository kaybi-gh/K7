using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
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

        var dialog = await DialogService.ShowAsync<Dialogs.CreateLibraryDialog>("Nouvelle librairie", options);
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

    private static string GetMediaTypeLabel(LibraryMediaType type) => type switch
    {
        LibraryMediaType.Movie => "Films",
        LibraryMediaType.Serie => "Séries",
        LibraryMediaType.Music => "Musique",
        _ => type.ToString()
    };

    private async Task IndexLibrary(LibraryDto library)
    {
        try
        {
            await K7ServerService.IndexLibraryFilesAsync(library.Id);
            Snackbar.Add($"Indexation de « {library.Title} » lancée.", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Erreur : {ex.Message}", Severity.Error);
        }
    }

    private async Task OpenUsersDialog(LibraryDto library)
    {
        var parameters = new DialogParameters<AdminLibraryUsersDialog>
        {
            { x => x.LibraryId, library.Id }
        };

        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        await DialogService.ShowAsync<AdminLibraryUsersDialog>($"Accès — {library.Title}", parameters, options);
    }
}
