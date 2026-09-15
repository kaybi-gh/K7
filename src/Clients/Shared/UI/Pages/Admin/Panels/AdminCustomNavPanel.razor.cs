using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminCustomNavPanel
{
    [Inject] private IServerPreferencesService ServerPreferences { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private ICustomNavStore CustomNavStore { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private CustomNavLayoutEditModel _model = new();
    private List<LibraryGroupDto> _groups = [];
    private bool _isLoading = true;
    private bool _saving;
    private readonly SettingsFormTracker<CustomNavLayoutEditModel> _formTracker = new();

    private bool IsDirty => _formTracker.IsDirty(_model);
    private bool ResetDisabled { get => !IsDirty && !field; set; }

    protected override async Task OnInitializedAsync()
    {
        try
        {
            _groups = await LibraryService.GetLibraryGroupsAsync();
            await CustomNavStore.EnsureLoadedAsync();
            _model = CustomNavLayoutEditModel.FromDto(await ServerPreferences.GetEffectiveServerCustomNavLayoutAsync());
            _formTracker.Capture(_model);
            ResetDisabled = await ServerPreferences.GetServerCustomNavLayoutAsync() is not null;
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

    private void CancelChanges() => _model = _formTracker.Restore();

    private async Task SaveAsync()
    {
        _saving = true;
        try
        {
            await ServerPreferences.UpdateServerCustomNavLayoutAsync(_model.ToDto());
            _formTracker.Capture(_model);
            ResetDisabled = true;
            await CustomNavStore.ReloadAsync();
            Snackbar.Add(L["SaveSuccess"], K7Severity.Success);
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

    private async Task ResetAsync()
    {
        _saving = true;
        try
        {
            await ServerPreferences.DeleteServerCustomNavLayoutAsync();
            _model = CustomNavLayoutEditModel.FromDto(await ServerPreferences.GetEffectiveServerCustomNavLayoutAsync());
            _formTracker.Capture(_model);
            ResetDisabled = false;
            await CustomNavStore.ReloadAsync();
            Snackbar.Add(L["ResetSuccess"], K7Severity.Success);
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
}
