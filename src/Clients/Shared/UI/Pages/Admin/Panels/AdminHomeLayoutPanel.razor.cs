using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos.Home;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminHomeLayoutPanel
{
    private sealed record HomeFormState(List<HomeRowEditModel> Rows);

    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private IHomeFeedStore HomeFeedStore { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Home> HomeL { get; set; } = default!;

    private List<HomeRowEditModel> _rows = [];
    private bool _isLoading = true;
    private bool _saving;
    private readonly SettingsFormTracker<HomeFormState> _formTracker = new();

    private bool IsDirty => _formTracker.IsDirty(new HomeFormState(_rows));
    private bool ResetDisabled { get => !IsDirty && !field; set; }

    protected override Task OnInitializedAsync() => LoadLayoutAsync();

    private async Task LoadLayoutAsync()
    {
        try
        {
            var layout = await ServerPreferencesService.GetEffectiveServerHomeLayoutAsync();
            _rows = layout.Rows.OrderBy(r => r.Order).Select(HomeRowEditModel.FromDto).ToList();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void CaptureFormState() => _formTracker.Capture(new HomeFormState(_rows));

    private void CancelChanges()
    {
        _rows = _formTracker.Restore().Rows;
    }

    private void RenumberRows()
    {
        for (var i = 0; i < _rows.Count; i++)
            _rows[i].Order = i;
        StateHasChanged();
    }

    private async Task SaveLayout()
    {
        _saving = true;
        try
        {
            RenumberRows();
            var layout = new HomeLayoutDto { Rows = _rows.Select(m => m.ToDto()).ToList() };
            await ServerPreferencesService.UpdateServerHomeLayoutAsync(layout);
            CaptureFormState();
            await RefreshOverrideStateAsync();
            await HomeFeedStore.ResetAndReloadAsync();
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

    private async Task ResetLayout()
    {
        _saving = true;
        try
        {
            await ServerPreferencesService.DeleteServerHomeLayoutAsync();
            var layout = await ServerPreferencesService.GetEffectiveServerHomeLayoutAsync();
            _rows = layout.Rows.OrderBy(r => r.Order).Select(HomeRowEditModel.FromDto).ToList();
            CaptureFormState();
            await RefreshOverrideStateAsync();
            await HomeFeedStore.ResetAndReloadAsync();
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

    private string GetRowTitle(string rowTitle) => HomeLayoutRowTitleHelper.Localize(HomeL, rowTitle);

    private async Task RefreshOverrideStateAsync() =>
        ResetDisabled = await ServerPreferenceOverrideHelper.HasHomeLayoutOverrideAsync(ServerPreferencesService);
}
