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
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    private const string BasePath = "/admin/library-groups";

    private bool _isLoading = true;
    private List<LibraryGroupDto>? _groups;
    private List<LibraryDto> _libraries = [];

    /// <summary>Group whose sheet is open. Null shows the list (plus a default sheet on wide screens).</summary>
    [Parameter] public Guid? GroupId { get; set; }

    private List<LibraryGroupDto> Groups => _groups ?? [];

    /// <summary>The group from the address, else the first of the list.</summary>
    private LibraryGroupDto? SelectedGroup =>
        Groups.FirstOrDefault(g => g.Id == GroupId) ?? Groups.FirstOrDefault();

    private bool IsGroupOpen => GroupId is not null && Groups.Any(g => g.Id == GroupId);

    protected override async Task OnInitializedAsync()
    {
        await LoadGroups();
    }

    private async Task LoadGroups()
    {
        // Only the first load swaps the page for a progress bar: a reload keeps the sheet in place.
        _isLoading = _groups is null;
        var groupsTask = LibraryService.GetLibraryGroupsAsync();
        var librariesTask = LibraryService.GetLibrariesAsync();
        await Task.WhenAll(groupsTask, librariesTask);
        _groups = await groupsTask;
        _libraries = await librariesTask;
        _isLoading = false;
    }

    private void GoToLibraries() => NavigationManager.NavigateTo("/admin/libraries");

    private async Task OpenEditDialog(LibraryGroupDto group)
    {
        var parameters = new K7DialogParameters
        {
            [nameof(EditLibraryGroupDialog.Group)] = group
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };

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

    private static string GetGroupIcon(LibraryGroupDto group) =>
        string.IsNullOrWhiteSpace(group.Icon) ? GetMediaTypeIcon(group.MediaType) : group.Icon;

    private static string? GetCoverUrl(LibraryGroupDto group) =>
        group.CoverPictureId is { } pictureId ? $"/api/metadata-pictures/{pictureId}?size=Medium" : null;

    private List<LibraryDto> GetLibraries(LibraryGroupDto group) =>
        _libraries
            .Where(l => group.LibraryIds.Contains(l.Id))
            .OrderBy(l => l.Title, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

    private List<string> GetLibraryTitles(LibraryGroupDto group) =>
        GetLibraries(group).Select(l => l.Title).ToList();

    private string GetLibrariesSummary(LibraryGroupDto group)
    {
        var titles = GetLibraryTitles(group);
        return titles.Count > 0 ? string.Join(", ", titles) : L["NoLibraries"];
    }

    private IReadOnlyList<string> GetLibraryBadges(LibraryGroupDto group) =>
        group.LibraryIds.Count > 0
            ? [string.Format(L["BadgeLibraries"], group.LibraryIds.Count)]
            : [];

    private string GetTapActionLabel(ExploreTapAction action) => action switch
    {
        ExploreTapAction.Browse => L["TapActionBrowse"],
        _ => L["TapActionSuggestions"]
    };

    private string GetMediaTypeLabel(LibraryMediaType type) => type switch
    {
        LibraryMediaType.Movie => L["TypeMovies"],
        LibraryMediaType.Serie => L["TypeSeries"],
        LibraryMediaType.Music => L["TypeMusic"],
        _ => type.ToString()
    };
}
