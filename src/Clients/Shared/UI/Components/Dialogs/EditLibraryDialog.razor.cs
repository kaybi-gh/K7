using K7.Shared.Dtos;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditLibraryDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [Parameter] public Guid LibraryId { get; set; }
    [Parameter] public string Title { get; set; } = "";
    [Parameter] public List<MetadataProviderInfoDto> AvailableProviders { get; set; } = [];
    [Parameter] public string? SelectedProvider { get; set; }
    [Parameter] public int? MetadataRefreshIntervalDays { get; set; }
    [Parameter] public string MetadataLanguage { get; set; } = "en";
    [Parameter] public string MetadataFallbackLanguage { get; set; } = "en";

    private bool _isSubmitting;

    private bool CanSubmit => !_isSubmitting && !string.IsNullOrWhiteSpace(Title) && !string.IsNullOrWhiteSpace(SelectedProvider);

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
                MetadataRefreshIntervalDays = MetadataRefreshIntervalDays
            };

            await LibraryService.UpdateLibraryAsync(LibraryId, request);

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
