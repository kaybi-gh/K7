using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class CreateLibraryDialog
{
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = default!;

    private int _activeStep;
    private LibraryMediaType _selectedMediaType;
    private bool _mediaTypeSelected;
    private string _title = "";
    private string _rootPath = "";
    private bool _triggerIndexing = true;
    private bool _isSubmitting;

    private void SelectMediaType(LibraryMediaType mediaType)
    {
        _selectedMediaType = mediaType;
        _mediaTypeSelected = true;
    }

    private string GetMediaTypeCardClass(LibraryMediaType type)
    {
        return _mediaTypeSelected && _selectedMediaType == type
            ? "border-4 mud-border-primary"
            : "";
    }

    private Color GetMediaTypeColor(LibraryMediaType type)
    {
        return _mediaTypeSelected && _selectedMediaType == type
            ? Color.Primary
            : Color.Default;
    }

    private string GetMediaTypeLabel(LibraryMediaType type) => type switch
    {
        LibraryMediaType.Movie => S["MediaTypeMovies"],
        LibraryMediaType.Serie => S["MediaTypeSeries"],
        LibraryMediaType.Music => S["MediaTypeMusic"],
        _ => type.ToString()
    };

    private bool CanAdvance() => _activeStep switch
    {
        0 => _mediaTypeSelected,
        1 => !string.IsNullOrWhiteSpace(_title) && !string.IsNullOrWhiteSpace(_rootPath),
        _ => true
    };

    private void NextStep()
    {
        if (CanAdvance() && _activeStep < 2)
            _activeStep++;
    }

    private void PreviousStep()
    {
        if (_activeStep > 0)
            _activeStep--;
    }

    private async Task BrowseFolderAsync()
    {
        var parameters = new DialogParameters<FolderBrowserDialog>
        {
            { x => x.InitialPath, string.IsNullOrWhiteSpace(_rootPath) ? null : _rootPath }
        };

        var options = new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        };

        var dialog = await DialogService.ShowAsync<FolderBrowserDialog>(L["SelectFolderTitle"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false, Data: string selectedPath })
        {
            _rootPath = selectedPath;
        }
    }

    private async Task SubmitAsync()
    {
        _isSubmitting = true;
        StateHasChanged();

        try
        {
            var request = new CreateLibraryRequest
            {
                Title = _title.Trim(),
                MediaType = _selectedMediaType,
                RootPath = _rootPath.Trim(),
                TriggerFileIndexingOnCreation = _triggerIndexing
            };

            await K7ServerService.CreateLibraryAsync(request);
            Snackbar.Add(string.Format(L["Success"], _title), Severity.Success);
            MudDialog.Close(DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(L["Error"], ex.Message), Severity.Error);
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    private void Cancel() => MudDialog.Cancel();
}
