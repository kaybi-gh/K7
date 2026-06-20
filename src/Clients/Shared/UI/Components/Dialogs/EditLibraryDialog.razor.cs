using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
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
    [Parameter] public LibraryMediaType MediaType { get; set; }
    [Parameter] public List<MetadataProviderInfoDto> AvailableProviders { get; set; } = [];
    [Parameter] public string? SelectedProvider { get; set; }
    [Parameter] public int? MetadataRefreshIntervalDays { get; set; }
    [Parameter] public string MetadataLanguage { get; set; } = "en";
    [Parameter] public string MetadataFallbackLanguage { get; set; } = "en";
    [Parameter] public bool IsFederated { get; set; }
    [Parameter] public List<LibraryGroupDto> AvailableGroups { get; set; } = [];
    [Parameter] public Guid SelectedGroupId { get; set; }
    [Parameter] public bool IntroDetectionEnabled { get; set; } = true;
    [Parameter] public bool SeekbarThumbnailGenerationEnabled { get; set; } = true;
    [Parameter] public bool MusicAudioAnalysisEnabled { get; set; } = true;
    [Parameter] public bool TranscodingEnabled { get; set; } = true;
    [Parameter] public bool TransmuxingEnabled { get; set; } = true;

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
                MetadataRefreshIntervalDays = MetadataRefreshIntervalDays,
                LibraryGroupId = SelectedGroupId,
                IntroDetectionEnabled = IntroDetectionEnabled,
                SeekbarThumbnailGenerationEnabled = SeekbarThumbnailGenerationEnabled,
                MusicAudioAnalysisEnabled = MusicAudioAnalysisEnabled,
                TranscodingEnabled = TranscodingEnabled,
                TransmuxingEnabled = TransmuxingEnabled
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
