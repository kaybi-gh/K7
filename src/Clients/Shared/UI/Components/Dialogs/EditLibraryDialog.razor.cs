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
    [Parameter] public bool RealtimeMonitorEnabled { get; set; } = true;
    [Parameter] public int AutoScanIntervalHours { get; set; } = 6;

    private string _title = "";
    private Guid _selectedGroupId;
    private string _selectedProvider = "";
    private string _metadataLanguage = "en";
    private string _metadataFallbackLanguage = "en";
    private int? _metadataRefreshIntervalDays;
    private bool _introDetectionEnabled = true;
    private bool _seekbarThumbnailGenerationEnabled = true;
    private bool _musicAudioAnalysisEnabled = true;
    private bool _transcodingEnabled = true;
    private bool _transmuxingEnabled = true;
    private bool _realtimeMonitorEnabled = true;
    private int _autoScanIntervalHours = 6;
    private bool _isSubmitting;

    protected override void OnInitialized()
    {
        _title = Title;
        _selectedGroupId = SelectedGroupId;
        _selectedProvider = SelectedProvider ?? "";
        _metadataLanguage = MetadataLanguage;
        _metadataFallbackLanguage = MetadataFallbackLanguage;
        _metadataRefreshIntervalDays = MetadataRefreshIntervalDays;
        _introDetectionEnabled = IntroDetectionEnabled;
        _seekbarThumbnailGenerationEnabled = SeekbarThumbnailGenerationEnabled;
        _musicAudioAnalysisEnabled = MusicAudioAnalysisEnabled;
        _transcodingEnabled = TranscodingEnabled;
        _transmuxingEnabled = TransmuxingEnabled;
        _realtimeMonitorEnabled = RealtimeMonitorEnabled;
        _autoScanIntervalHours = AutoScanIntervalHours;
    }

    private bool CanSubmit => !_isSubmitting && !string.IsNullOrWhiteSpace(_title) && !string.IsNullOrWhiteSpace(_selectedProvider);

    private async Task Submit()
    {
        var request = new UpdateLibraryRequest
        {
            Title = _title.Trim(),
            MetadataProviderName = _selectedProvider,
            MetadataLanguage = _metadataLanguage,
            MetadataFallbackLanguage = _metadataFallbackLanguage,
            MetadataRefreshIntervalDays = _metadataRefreshIntervalDays,
            LibraryGroupId = _selectedGroupId,
            IntroDetectionEnabled = _introDetectionEnabled,
            SeekbarThumbnailGenerationEnabled = _seekbarThumbnailGenerationEnabled,
            MusicAudioAnalysisEnabled = _musicAudioAnalysisEnabled,
            TranscodingEnabled = _transcodingEnabled,
            TransmuxingEnabled = _transmuxingEnabled,
            RealtimeMonitorEnabled = _realtimeMonitorEnabled,
            AutoScanIntervalHours = _autoScanIntervalHours
        };

        _isSubmitting = true;
        StateHasChanged();

        try
        {
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
