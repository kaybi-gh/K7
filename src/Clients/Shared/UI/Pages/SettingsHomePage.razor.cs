using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Pages;
using K7.Shared.Dtos.Home;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Pages;

public partial class SettingsHomePage
{
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IStringLocalizer<Home> HomeL { get; set; } = default!;

    private List<HomeRowEditModel> _rows = [];
    private bool _isLoading = true;
    private bool _saving;
    private HomeLayoutPreview? _preview;
    private bool _previewLoading;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            var layout = await PreferencesService.GetHomeLayoutAsync();
            _rows = layout.Rows.OrderBy(r => r.Order).Select(HomeRowEditModel.FromDto).ToList();
        }
        catch
        {
            _rows = [];
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void MoveUp(HomeRowEditModel row)
    {
        var index = _rows.IndexOf(row);
        if (index <= 0)
            return;
        _rows.RemoveAt(index);
        _rows.Insert(index - 1, row);
        RenumberRows();
    }

    private void MoveDown(HomeRowEditModel row)
    {
        var index = _rows.IndexOf(row);
        if (index >= _rows.Count - 1)
            return;
        _rows.RemoveAt(index);
        _rows.Insert(index + 1, row);
        RenumberRows();
    }

    private void RenumberRows()
    {
        for (var i = 0; i < _rows.Count; i++)
            _rows[i].Order = i;
    }

    private async Task SaveLayout()
    {
        _saving = true;
        try
        {
            RenumberRows();
            var layout = new HomeLayoutDto { Rows = _rows.Select(m => m.ToDto()).ToList() };
            await PreferencesService.UpdateHomeLayoutAsync(layout);
            Snackbar.Add(L["HomeSaveSuccess"], K7Severity.Success);
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
            await PreferencesService.ResetHomeLayoutAsync();
            var layout = await PreferencesService.GetHomeLayoutAsync();
            _rows = layout.Rows.OrderBy(r => r.Order).Select(HomeRowEditModel.FromDto).ToList();
            Snackbar.Add(L["HomeResetSuccess"], K7Severity.Success);
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

    private async Task RefreshPreviewAsync()
    {
        if (_preview is null) return;
        _previewLoading = true;
        StateHasChanged();
        await _preview.RefreshAsync();
        _previewLoading = false;
    }

    private string GetRowTitle(string rowTitle) => HomeLayoutRowTitleHelper.Localize(HomeL, rowTitle);
}
