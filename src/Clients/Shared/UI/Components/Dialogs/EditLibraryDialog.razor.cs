using K7.Clients.Shared.Models;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditLibraryDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [Parameter] public Guid LibraryId { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public string? Description { get; set; }
    [Parameter] public string? Icon { get; set; }
    [Parameter] public Guid? CoverPictureId { get; set; }
    [Parameter] public List<MetadataProviderInfoDto> AvailableProviders { get; set; } = [];
    [Parameter] public string? SelectedProvider { get; set; }
    [Parameter] public int? MetadataRefreshIntervalDays { get; set; }
    [Parameter] public string MetadataLanguage { get; set; } = "en";
    [Parameter] public string MetadataFallbackLanguage { get; set; } = "en";

    private Guid? _currentCoverPictureId;
    private CoverPickerResult? _pendingCover;
    private bool _isSubmitting;
    private List<LibraryPictureDto> _libraryPictures = [];

    protected override void OnParametersSet()
    {
        _currentCoverPictureId = CoverPictureId;
    }

    protected override async Task OnInitializedAsync()
    {
        _libraryPictures = await LibraryService.GetLibraryPicturesAsync(LibraryId);
    }

    private async Task OpenIconPicker()
    {
        var parameters = new K7DialogParameters<K7IconPickerDialog>();
        parameters.Add(x => x.InitialValue, Icon);
        parameters.Add(x => x.SearchPlaceholder, L["IconSearch"].Value);
        parameters.Add(x => x.CancelText, L["Cancel"].Value);
        parameters.Add(x => x.ConfirmText, L["Save"].Value);
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7IconPickerDialog>(L["IconLabel"].Value, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
            Icon = result.Data as string;
    }

    private async Task OpenCoverPicker()
    {
        var parameters = new K7DialogParameters<K7CoverPickerDialog>();
        parameters.Add(x => x.Pictures, _libraryPictures);
        parameters.Add(x => x.FromMediaText, L["CoverFromMedia"].Value);
        parameters.Add(x => x.UploadText, L["CoverUpload"].Value);
        parameters.Add(x => x.ChooseFileText, L["UploadCover"].Value);
        parameters.Add(x => x.NoPicturesText, L["NoPictures"].Value);
        parameters.Add(x => x.CancelText, L["Cancel"].Value);
        parameters.Add(x => x.ConfirmText, L["Save"].Value);
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7CoverPickerDialog>(L["CoverLabel"].Value, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: CoverPickerResult picked })
            _pendingCover = picked;
    }

    private bool CanSubmit => !_isSubmitting && !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(SelectedProvider);

    private void RemoveCover()
    {
        _currentCoverPictureId = null;
        _pendingCover = null;
    }

    private async Task Submit()
    {
        _isSubmitting = true;
        StateHasChanged();

        try
        {
            var request = new UpdateLibraryRequest
            {
                Title = Title.Trim(),
                MetadataProviderName = SelectedProvider,
                MetadataLanguage = MetadataLanguage,
                MetadataFallbackLanguage = MetadataFallbackLanguage,
                MetadataRefreshIntervalDays = MetadataRefreshIntervalDays,
                Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
                Icon = Icon
            };

            await LibraryService.UpdateLibraryAsync(LibraryId, request);

            if (_pendingCover?.SourcePictureId.HasValue == true)
            {
                await LibraryService.SetLibraryCoverFromPictureAsync(LibraryId, _pendingCover.SourcePictureId.Value);
            }
            else if (_pendingCover?.File is { } file)
            {
                const long maxSize = 10 * 1024 * 1024;
                await using var stream = file.OpenReadStream(maxSize);
                await LibraryService.UploadLibraryCoverAsync(LibraryId, stream, file.Name);
            }

            Dialog.Close(K7DialogResult.Ok(true));
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
            _isSubmitting = false;
        }
    }

    private void Cancel() => Dialog.Cancel();
}
