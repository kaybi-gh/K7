using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Extensions;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminLibraryGroupsPanel
{
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _isLoading = true;
    private List<LibraryGroupDto>? _groups;

    protected override async Task OnInitializedAsync()
    {
        await LoadGroups();
    }

    private async Task LoadGroups()
    {
        _isLoading = true;
        _groups = await LibraryService.GetLibraryGroupsAsync();
        _isLoading = false;
    }

    private async Task OpenEditDialog(LibraryGroupDto group)
    {
        var parameters = new K7DialogParameters
        {
            [nameof(EditLibraryGroupDialog.Group)] = group
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };

        var dialog = await DialogService.ShowAsync<EditLibraryGroupDialog>(group.Title, parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            await LoadGroups();
            Snackbar.Add(L["GroupUpdated"], K7Severity.Success);
        }
    }

    private async Task ConfirmDelete(LibraryGroupDto group)
    {
        if (group.LibraryIds.Count > 0)
        {
            Snackbar.Add(L["CannotDeleteNonEmpty"], K7Severity.Warning);
            return;
        }

        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteConfirmTitle"],
            string.Format(L["DeleteConfirmMessage"], group.Title),
            yesText: S["Delete"],
            noText: S["Cancel"]);

        if (confirmed == true)
        {
            try
            {
                await LibraryService.DeleteLibraryGroupAsync(group.Id);
                await LoadGroups();
                Snackbar.Add(L["GroupDeleted"], K7Severity.Success);
            }
            catch (Exception ex)
            {
                Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            }
        }
    }

    private static string GetMediaTypeIcon(LibraryMediaType type) => type switch
    {
        LibraryMediaType.Movie => Phosphor.FilmSlate,
        LibraryMediaType.Serie => Phosphor.Television,
        LibraryMediaType.Music => Phosphor.MusicNote,
        _ => Phosphor.Folder
    };

    private string GetMediaTypeLabel(LibraryMediaType type) => type switch
    {
        LibraryMediaType.Movie => L["TypeMovies"],
        LibraryMediaType.Serie => L["TypeSeries"],
        LibraryMediaType.Music => L["TypeMusic"],
        _ => type.ToString()
    };
}
