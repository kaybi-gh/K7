using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class CreateLibraryDialog
{
    [Inject] private ILibraryService K7ServerService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    private int _activeStep;
    private LibraryMediaType _selectedMediaType;
    private bool _mediaTypeSelected;
    private string _title = "";
    private string _rootPath = "";
    private string _selectedProvider = "";
    private List<MetadataProviderInfoDto> _availableProviders = [];
    private bool _triggerIndexing = true;
    private bool _isSubmitting;

    private async Task SelectMediaType(LibraryMediaType mediaType)
    {
        _selectedMediaType = mediaType;
        _mediaTypeSelected = true;

        _availableProviders = await K7ServerService.GetMetadataProvidersAsync(mediaType);
        _selectedProvider = _availableProviders.FirstOrDefault()?.ProviderName ?? "";
    }

    private async Task OnMediaTypeKeyDown(KeyboardEventArgs e, LibraryMediaType mediaType)
    {
        if (e.Code is "Enter" or "Space")
        {
            await SelectMediaType(mediaType);
        }
    }

    private string GetMediaTypeCardClass(LibraryMediaType type)
    {
        return _mediaTypeSelected && _selectedMediaType == type
            ? "border-4 border-accent"
            : "";
    }

    private string GetMediaTypeColor(LibraryMediaType type)
    {
        return _mediaTypeSelected && _selectedMediaType == type
            ? "primary"
            : "default";
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
        1 => !string.IsNullOrWhiteSpace(_title) && !string.IsNullOrWhiteSpace(_rootPath) && !string.IsNullOrWhiteSpace(_selectedProvider),
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
        var parameters = new K7DialogParameters<FolderBrowserDialog>
        {
            { x => x.InitialPath, string.IsNullOrWhiteSpace(_rootPath) ? null : _rootPath }
        };

        var options = new K7DialogOptions {
            MaxWidth = K7DialogMaxWidth.Small,
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
                TriggerFileIndexingOnCreation = _triggerIndexing,
                MetadataProviderName = _selectedProvider
            };

            await K7ServerService.CreateLibraryAsync(request);
            Snackbar.Add(string.Format(L["Success"], _title), K7Severity.Success);
            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(L["Error"], ex.Message), K7Severity.Error);
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    private void Cancel() => Dialog.Cancel();
}
