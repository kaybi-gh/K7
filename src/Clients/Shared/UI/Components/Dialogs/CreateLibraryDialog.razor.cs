using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
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
    private int _maxVisitedStep;
    private IReadOnlyList<string> _stepLabels => [L["Step1"].Value, L["Step2"].Value, L["Step3"].Value];
    private LibraryMediaType _selectedMediaType;
    private bool _mediaTypeSelected;
    private string _title = "";
    private string? _groupDescription = null;
    private string? _groupIcon = null;
    private string _rootPath = "";
    private string _selectedProvider = "";
    private string _metadataLanguage = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
    private string _metadataFallbackLanguage = "en";
    private List<MetadataProviderInfoDto> _availableProviders = [];
    private bool _triggerIndexing = true;
    private bool _realtimeMonitorEnabled = true;
    private int _autoScanIntervalHours = 6;
    private int? _metadataRefreshIntervalDays;
    private bool _isSubmitting;
    private bool _introDetectionEnabled = true;
    private bool _themeSongGenerationEnabled = true;
    private bool _seekbarThumbnailGenerationEnabled = true;
    private bool _chapterExtractionEnabled = true;
    private bool _musicAudioAnalysisEnabled = true;
    private bool _transcodingEnabled = true;
    private bool _transmuxingEnabled = true;

    private List<LibraryGroupDto> _compatibleGroups = [];
    private Guid? _selectedGroupId;
    private bool _createNewGroup = true;

    private async Task SelectMediaType(LibraryMediaType mediaType)
    {
        _selectedMediaType = mediaType;
        _mediaTypeSelected = true;

        _availableProviders = await K7ServerService.GetMetadataProvidersAsync(mediaType);
        _selectedProvider = mediaType switch
        {
            LibraryMediaType.Serie => _availableProviders.FirstOrDefault(p => p.ProviderName == "tvdb")?.ProviderName
                ?? _availableProviders.FirstOrDefault()?.ProviderName
                ?? "",
            _ => _availableProviders.FirstOrDefault()?.ProviderName ?? ""
        };

        var allGroups = await K7ServerService.GetLibraryGroupsAsync();
        _compatibleGroups = allGroups.Where(g => g.MediaType == mediaType).ToList();
        _createNewGroup = _compatibleGroups.Count == 0;
        _selectedGroupId = null;
    }

    private void SelectGroup(Guid? groupId)
    {
        _selectedGroupId = groupId;
        _createNewGroup = groupId is null;
    }


    private string GetMediaTypeCardClass(LibraryMediaType type)
    {
        return _mediaTypeSelected && _selectedMediaType == type
            ? "k7-paper--selected"
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

    private void GoToStep(int step)
    {
        if (step <= _maxVisitedStep)
            _activeStep = step;
    }

    private void NextStep()
    {
        if (CanAdvance() && _activeStep < 2)
        {
            _activeStep++;
            if (_activeStep > _maxVisitedStep)
                _maxVisitedStep = _activeStep;
        }
    }

    private void PreviousStep()
    {
        if (_activeStep > 0)
            _activeStep--;
    }

    private async Task OpenIconPickerAsync()
    {
        var parameters = new K7DialogParameters<K7IconPickerDialog>();
        parameters.Add(x => x.InitialValue, _groupIcon);
        parameters.Add(x => x.SearchPlaceholder, L["IconSearch"].Value);
        parameters.Add(x => x.CancelText, S["Cancel"].Value);
        parameters.Add(x => x.ConfirmText, S["Confirm"].Value);
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7IconPickerDialog>(L["IconLabel"].Value, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
            _groupIcon = result.Data as string;
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
        var request = new CreateLibraryRequest
        {
            Title = _title.Trim(),
            MediaType = _selectedMediaType,
            RootPath = _rootPath.Trim(),
            TriggerFileIndexingOnCreation = _triggerIndexing,
            MetadataProviderName = _selectedProvider,
            MetadataLanguage = _metadataLanguage,
            MetadataFallbackLanguage = _metadataFallbackLanguage,
            LibraryGroupId = _selectedGroupId,
            GroupDescription = _createNewGroup && !string.IsNullOrWhiteSpace(_groupDescription) ? _groupDescription.Trim() : null,
            GroupIcon = _createNewGroup ? _groupIcon : null,
            IntroDetectionEnabled = _introDetectionEnabled,
            ThemeSongGenerationEnabled = _themeSongGenerationEnabled,
            SeekbarThumbnailGenerationEnabled = _seekbarThumbnailGenerationEnabled,
            ChapterExtractionEnabled = _chapterExtractionEnabled,
            MusicAudioAnalysisEnabled = _musicAudioAnalysisEnabled,
            TranscodingEnabled = _transcodingEnabled,
            TransmuxingEnabled = _transmuxingEnabled,
            MetadataRefreshIntervalDays = _metadataRefreshIntervalDays,
            RealtimeMonitorEnabled = _realtimeMonitorEnabled,
            AutoScanIntervalHours = _autoScanIntervalHours
        };

        _isSubmitting = true;
        StateHasChanged();

        try
        {
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
