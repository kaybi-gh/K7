using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Pages.Admin.Dialogs;
using K7.Shared.Dtos.Home;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Panels;

public partial class AdminHomeLayoutPanel
{
    [Inject] private IServerPreferencesService ServerPreferencesService { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private List<HomeRowEditModel> _rows = [];
    private List<LibraryDto> _libraries = [];
    private bool _isLoading = true;
    private bool _saving;
    private HomeLayoutPreview? _preview;
    private bool _previewLoading;

    protected override async Task OnInitializedAsync()
    {
        var librariesTask = LibraryService.GetLibrariesAsync();
        await LoadLayoutAsync();
        _libraries = await librariesTask;
    }

    private async Task LoadLayoutAsync()
    {
        try
        {
            var layout = await ServerPreferencesService.GetEffectiveServerHomeLayoutAsync();
            _rows = layout.Rows.OrderBy(r => r.Order).Select(HomeRowEditModel.FromDto).ToList();
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

    private void DeleteRow(HomeRowEditModel row)
    {
        _rows.Remove(row);
        RenumberRows();
    }

    private async Task AddRow()
    {
        var parameters = new K7DialogParameters<AdminHomeRowDialog>();
        parameters.Add(d => d.Libraries, _libraries);
        var dialog = await DialogService.ShowAsync<AdminHomeRowDialog>(L["AddRow"], parameters);
        var result = await dialog.Result;
        if (result.Canceled)
            return;
        var model = (HomeRowEditModel)result.Data!;
        model.Order = _rows.Count;
        _rows.Add(model);
    }

    private async Task EditRow(HomeRowEditModel row)
    {
        var parameters = new K7DialogParameters<AdminHomeRowDialog>();
        parameters.Add(d => d.Libraries, _libraries);
        parameters.Add(d => d.InitialModel, row);
        var dialog = await DialogService.ShowAsync<AdminHomeRowDialog>(L["EditRow"], parameters);
        var result = await dialog.Result;
        if (result.Canceled)
            return;
        var updated = (HomeRowEditModel)result.Data!;
        updated.Order = row.Order;
        var index = _rows.IndexOf(row);
        _rows[index] = updated;
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
            await ServerPreferencesService.UpdateServerHomeLayoutAsync(layout);
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

    private async Task RefreshPreviewAsync()
    {
        if (_preview is null) return;
        _previewLoading = true;
        StateHasChanged();
        await _preview.RefreshAsync();
        _previewLoading = false;
    }
}

