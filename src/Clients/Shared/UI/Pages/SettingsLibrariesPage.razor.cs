using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsLibrariesPage
{
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _loading = true;
    private bool _saving;
    private List<LibraryDto> _libraries = [];
    private HashSet<Guid> _selfExcludedIds = [];

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var librariesTask = LibraryService.GetLibrariesAsync();
            var exclusionsTask = PreferencesService.GetSelfLibraryExclusionsAsync();
            await Task.WhenAll(librariesTask, exclusionsTask);
            _libraries = librariesTask.Result;
            _selfExcludedIds = exclusionsTask.Result.ToHashSet();
        }
        catch
        {
            _libraries = [];
        }
        _loading = false;
    }

    private async Task ToggleLibrary(Guid libraryId, bool exclude)
    {
        if (exclude)
            _selfExcludedIds.Add(libraryId);
        else
            _selfExcludedIds.Remove(libraryId);

        _saving = true;
        try
        {
            await PreferencesService.UpdateSelfLibraryExclusionsAsync(new UpdateSelfLibraryExclusionsRequest
            {
                ExcludedLibraryIds = _selfExcludedIds.ToList()
            });
            Snackbar.Add(L["LibrariesSaveSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
            if (exclude)
                _selfExcludedIds.Remove(libraryId);
            else
                _selfExcludedIds.Add(libraryId);
        }
        finally
        {
            _saving = false;
        }
    }
}
