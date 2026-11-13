using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Constants;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsCustomNavPage : IDisposable
{
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private ICustomNavStore CustomNavStore { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private CustomNavLayoutEditModel _model = new();
    private List<LibraryGroupDto> _groups = [];
    private bool _isLoading = true;
    private readonly SettingsFormTracker<CustomNavLayoutEditModel> _formTracker = new();
    private readonly SettingsAutoSaveSession<CustomNavLayoutEditModel> _autoSave;

    public SettingsCustomNavPage()
    {
        _autoSave = new SettingsAutoSaveSession<CustomNavLayoutEditModel>(
            _formTracker,
            () => _isLoading ? null : _model,
            PersistAsync,
            work => InvokeAsync(work),
            StateHasChanged,
            ReportError);
    }

    /// <summary>Set to whether the user has a layout of their own, read as whether reset has nothing to do.</summary>
    private bool ResetDisabled { get => !_autoSave.IsDirty && !field; set; }

    public void Dispose() => _autoSave.Dispose();

    protected override void OnAfterRender(bool firstRender) => _autoSave.OnAfterRender();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _groups = await LibraryService.GetLibraryGroupsAsync();
            await CustomNavStore.EnsureLoadedAsync();
            _model = CustomNavLayoutEditModel.FromDto(await PreferencesService.GetCustomNavLayoutAsync());
            _formTracker.Capture(_model);
            ResetDisabled = await PreferencesService.UserSettingExistsAsync(UserPreferenceKeys.CustomNavLayout);
        }
        catch
        {
            _model = CustomNavLayoutEditModel.FromDto(CustomNavLayoutDto.Disabled());
            _formTracker.Capture(_model);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private async Task PersistAsync()
    {
        await PreferencesService.UpdateCustomNavLayoutAsync(_model.ToDto());
        ResetDisabled = true;
        await CustomNavStore.ReloadAsync();
    }

    private Task ResetAsync() => _autoSave.RunAsync(async () =>
    {
        await PreferencesService.ResetCustomNavLayoutAsync();
        _model = CustomNavLayoutEditModel.FromDto(await PreferencesService.GetCustomNavLayoutAsync());
        _formTracker.Capture(_model);
        ResetDisabled = false;
        await CustomNavStore.ReloadAsync();
        Snackbar.Add(L["ResetSuccess"], K7Severity.Success);
    });

    private void ReportError(Exception ex) =>
        Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
}
