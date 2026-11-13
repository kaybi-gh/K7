using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsLibrariesPage : IDisposable
{
    private sealed record TapActionState(Guid GroupId, ExploreTapAction Action);
    private sealed record LibrariesFormState(List<Guid> ExcludedLibraryIds, List<TapActionState> TapActions);

    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _loading = true;
    private List<LibraryGroupDto> _groups = [];
    private List<LibraryDto> _libraries = [];
    private HashSet<Guid> _selfExcludedIds = [];
    private Dictionary<Guid, ExploreTapAction> _tapActions = [];
    private readonly SettingsFormTracker<LibrariesFormState> _formTracker = new();
    private readonly SettingsAutoSaveSession<LibrariesFormState> _autoSave;

    public SettingsLibrariesPage()
    {
        _autoSave = new SettingsAutoSaveSession<LibrariesFormState>(
            _formTracker,
            () => _loading ? null : CurrentFormState(),
            PersistAsync,
            work => InvokeAsync(work),
            StateHasChanged,
            ReportError);
    }

    private IReadOnlyList<ButtonGroupOption<ExploreTapAction>> TapActionOptions =>
    [
        new(ExploreTapAction.Suggestions, L["ExploreTapSuggestions"]),
        new(ExploreTapAction.Browse, L["ExploreTapBrowse"])
    ];

    private bool IsDirty => _formTracker.IsDirty(CurrentFormState());

    private bool ResetDisabled => !IsDirty && _selfExcludedIds.Count == 0 && !HasTapActionOverrides();

    public void Dispose() => _autoSave.Dispose();

    protected override void OnAfterRender(bool firstRender) => _autoSave.OnAfterRender();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var groupsTask = LibraryService.GetLibraryGroupsAsync();
            var librariesTask = LibraryService.GetLibrariesAsync();
            var exclusionsTask = PreferencesService.GetSelfLibraryExclusionsAsync();
            var preferencesTask = PreferencesService.GetEffectiveGeneralPreferencesAsync();
            await Task.WhenAll(groupsTask, librariesTask, exclusionsTask, preferencesTask);
            _groups = groupsTask.Result;
            _libraries = librariesTask.Result;
            _selfExcludedIds = exclusionsTask.Result.ToHashSet();
            ApplyTapActions(preferencesTask.Result);
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

    private ExploreTapAction GetTapAction(Guid groupId) =>
        _tapActions.GetValueOrDefault(groupId, ExploreTapAction.Suggestions);

    private void SetTapAction(Guid groupId, ExploreTapAction action)
    {
        _tapActions[groupId] = action;
        StateHasChanged();
    }

    private void ApplyTapActions(GeneralPreferencesDto preferences)
    {
        _tapActions = _groups.ToDictionary(
            group => group.Id,
            group => preferences.ResolveExploreTapAction(group.Id, group.ExploreTapAction));
    }

    private LibrariesFormState CurrentFormState() =>
        new(
            _selfExcludedIds.OrderBy(id => id).ToList(),
            _tapActions
                .OrderBy(pair => pair.Key)
                .Select(pair => new TapActionState(pair.Key, pair.Value))
                .ToList());

    private void CaptureFormState() => _formTracker.Capture(CurrentFormState());

    private void ToggleLibrary(Guid libraryId, bool exclude)
    {
        if (exclude)
            _selfExcludedIds.Add(libraryId);
        else
            _selfExcludedIds.Remove(libraryId);

        StateHasChanged();
    }

    private bool HasTapActionOverrides() =>
        _groups.Any(group => GetTapAction(group.Id) != group.ExploreTapAction);

    private Dictionary<Guid, ExploreTapAction> BuildTapActionOverrides()
    {
        var overrides = new Dictionary<Guid, ExploreTapAction>();
        foreach (var group in _groups)
        {
            var action = GetTapAction(group.Id);
            if (action != group.ExploreTapAction)
                overrides[group.Id] = action;
        }

        return overrides;
    }

    private async Task PersistAsync()
    {
        await PreferencesService.UpdateSelfLibraryExclusionsAsync(new UpdateSelfLibraryExclusionsRequest
        {
            ExcludedLibraryIds = _selfExcludedIds.ToList()
        });

        var overrides = BuildTapActionOverrides();
        if (overrides.Count == 0)
            await PreferencesService.ResetUserGeneralPreferencesAsync();
        else
            await PreferencesService.UpdateUserGeneralPreferencesAsync(new GeneralPreferencesDto
            {
                ExploreTapActions = overrides
            });
    }

    private void ReportError(Exception ex) =>
        Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);

    private async Task ResetToDefaultsAsync()
    {
        _selfExcludedIds.Clear();
        foreach (var group in _groups)
            _tapActions[group.Id] = group.ExploreTapAction;

        await _autoSave.SaveAsync();
    }
}
