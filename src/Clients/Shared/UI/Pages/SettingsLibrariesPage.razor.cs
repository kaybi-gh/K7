using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsLibrariesPage
{
    private sealed record LibrariesFormState(List<Guid> ExcludedLibraryIds);

    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _loading = true;
    private bool _saving;
    private List<LibraryGroupDto> _groups = [];
    private List<LibraryDto> _libraries = [];
    private HashSet<Guid> _selfExcludedIds = [];
    private readonly SettingsFormTracker<LibrariesFormState> _formTracker = new();

    private bool IsDirty =>
        _formTracker.IsDirty(new LibrariesFormState(_selfExcludedIds.OrderBy(id => id).ToList()));

    private bool ResetDisabled => !IsDirty && _selfExcludedIds.Count == 0;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var groupsTask = LibraryService.GetLibraryGroupsAsync();
            var librariesTask = LibraryService.GetLibrariesAsync();
            var exclusionsTask = PreferencesService.GetSelfLibraryExclusionsAsync();
            await Task.WhenAll(groupsTask, librariesTask, exclusionsTask);
            _groups = groupsTask.Result;
            _libraries = librariesTask.Result;
            _selfExcludedIds = exclusionsTask.Result.ToHashSet();
            CaptureFormState();
        }
        catch
        {
            _groups = [];
            _libraries = [];
            CaptureFormState();
        }

        _loading = false;
    }

    private IEnumerable<LibraryDto> GetLibrariesForGroup(LibraryGroupDto group) =>
        _libraries.Where(l => l.LibraryGroupId == group.Id);

    private void CaptureFormState() =>
        _formTracker.Capture(new LibrariesFormState(_selfExcludedIds.OrderBy(id => id).ToList()));

    private void CancelChanges()
    {
        _selfExcludedIds = _formTracker.Restore().ExcludedLibraryIds.ToHashSet();
    }

    private void ToggleLibrary(Guid libraryId, bool exclude)
    {
        if (exclude)
            _selfExcludedIds.Add(libraryId);
        else
            _selfExcludedIds.Remove(libraryId);

        StateHasChanged();
    }

    private async Task SaveAsync()
    {
        if (_saving || !IsDirty)
            return;

        _saving = true;
        try
        {
            await PreferencesService.UpdateSelfLibraryExclusionsAsync(new UpdateSelfLibraryExclusionsRequest
            {
                ExcludedLibraryIds = _selfExcludedIds.ToList()
            });
            CaptureFormState();
            Snackbar.Add(L["LibrariesSaveSuccess"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _saving = false;
        }
    }

    private async Task ResetToDefaultsAsync()
    {
        _selfExcludedIds.Clear();
        await SaveAsync();
    }
}
