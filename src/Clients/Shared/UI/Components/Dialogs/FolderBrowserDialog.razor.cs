using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class FolderBrowserDialog
{
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter]
    public string? InitialPath { get; set; }

    private string? _currentPath;
    private List<DirectoryEntryDto>? _directories;
    private DirectoryEntryDto? _selectedEntry;
    private bool _isLoading;

    protected override async Task OnInitializedAsync()
    {
        _currentPath = InitialPath;
        await LoadDirectories(_currentPath);
    }

    private async Task LoadDirectories(string? path)
    {
        _isLoading = true;
        _selectedEntry = null;
        StateHasChanged();

        try
        {
            var result = await K7ServerService.GetDirectoriesAsync(path);
            if (result is not null)
            {
                _currentPath = result.Path;
                _directories = result.Directories.ToList();
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isLoading = false;
            StateHasChanged();
        }
    }

    private async Task NavigateInto(DirectoryEntryDto dir)
    {
        await LoadDirectories(dir.FullPath);
    }

    private void SelectDirectory(DirectoryEntryDto dir)
    {
        _selectedEntry = dir;
    }

    private void OnItemClick(DirectoryEntryDto dir)
    {
        _selectedEntry = dir;
    }

    private async Task NavigateUp()
    {
        if (string.IsNullOrEmpty(_currentPath))
            return;

        var parent = System.IO.Path.GetDirectoryName(_currentPath);
        await LoadDirectories(parent);
    }

    private string? GetSelectedPath()
    {
        if (_selectedEntry is not null)
            return _selectedEntry.FullPath;
        if (!string.IsNullOrEmpty(_currentPath))
            return _currentPath;
        return null;
    }

    private void Confirm()
    {
        var selectedPath = GetSelectedPath();
        if (!string.IsNullOrEmpty(selectedPath))
        {
            Dialog.Close(K7DialogResult.Ok(selectedPath));
        }
    }

    private void Cancel() => Dialog.Cancel();
}
