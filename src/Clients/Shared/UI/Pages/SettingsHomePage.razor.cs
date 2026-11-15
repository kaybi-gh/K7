using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos.Home;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsHomePage : IDisposable
{
    private sealed record HomeFormState(List<HomeRowEditModel> Rows);

    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IHomeFeedStore HomeFeedStore { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Home> HomeL { get; set; } = default!;

    private List<HomeRowEditModel> _rows = [];
    private bool _isLoading = true;
    private readonly SettingsFormTracker<HomeFormState> _formTracker = new();
    private readonly SettingsAutoSaveSession<HomeFormState> _autoSave;

    public SettingsHomePage()
    {
        _autoSave = new SettingsAutoSaveSession<HomeFormState>(
            _formTracker,
            () => _isLoading ? null : new HomeFormState(_rows),
            PersistAsync,
            work => InvokeAsync(work),
            StateHasChanged,
            ReportError);
    }

    private bool ResetDisabled { get => !_autoSave.IsDirty && !field; set; }

    public void Dispose() => _autoSave.Dispose();

    protected override void OnAfterRender(bool firstRender) => _autoSave.OnAfterRender();

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var layout = await PreferencesService.GetHomeLayoutAsync();
            _rows = layout.Rows.OrderBy(r => r.Order).Select(HomeRowEditModel.FromDto).ToList();
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }
        catch
        {
            _rows = [];
            CaptureFormState();
            await RefreshOverrideStateAsync();
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void CaptureFormState() => _formTracker.Capture(new HomeFormState(_rows));

    private async Task PersistAsync()
    {
        var layout = new HomeLayoutDto { Rows = _rows.Select(m => m.ToDto()).ToList() };
        await PreferencesService.UpdateHomeLayoutAsync(layout);
        await RefreshOverrideStateAsync();
        await HomeFeedStore.ResetAndReloadAsync();
    }

    private Task ResetLayoutAsync() => _autoSave.RunAsync(async () =>
    {
        await PreferencesService.ResetHomeLayoutAsync();
        var layout = await PreferencesService.GetHomeLayoutAsync();
        _rows = layout.Rows.OrderBy(r => r.Order).Select(HomeRowEditModel.FromDto).ToList();
        CaptureFormState();
        await RefreshOverrideStateAsync();
        await HomeFeedStore.ResetAndReloadAsync();
        Snackbar.Add(L["HomeResetSuccess"], K7Severity.Success);
    });

    private void ReportError(Exception ex) =>
        Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);

    private string GetRowTitle(string rowTitle) => HomeLayoutRowTitleHelper.Localize(HomeL, rowTitle);

    private async Task RefreshOverrideStateAsync() =>
        ResetDisabled = await UserPreferenceOverrideHelper.HasHomeLayoutOverrideAsync(PreferencesService);
}
