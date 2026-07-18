using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryProcessingSettingsSection
{
    [Parameter] public LibraryMediaType MediaType { get; set; }
    [Parameter] public bool IntroDetectionEnabled { get; set; } = true;
    [Parameter] public EventCallback<bool> IntroDetectionEnabledChanged { get; set; }
    [Parameter] public bool ThemeSongGenerationEnabled { get; set; } = true;
    [Parameter] public EventCallback<bool> ThemeSongGenerationEnabledChanged { get; set; }
    [Parameter] public bool SeekbarThumbnailGenerationEnabled { get; set; } = true;
    [Parameter] public EventCallback<bool> SeekbarThumbnailGenerationEnabledChanged { get; set; }
    [Parameter] public bool ChapterExtractionEnabled { get; set; } = true;
    [Parameter] public EventCallback<bool> ChapterExtractionEnabledChanged { get; set; }
    [Parameter] public bool MusicAudioAnalysisEnabled { get; set; } = true;
    [Parameter] public EventCallback<bool> MusicAudioAnalysisEnabledChanged { get; set; }
    [Parameter] public bool TranscodingEnabled { get; set; } = true;
    [Parameter] public EventCallback<bool> TranscodingEnabledChanged { get; set; }
    [Parameter] public bool TransmuxingEnabled { get; set; } = true;
    [Parameter] public EventCallback<bool> TransmuxingEnabledChanged { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public bool ShowSectionTitle { get; set; } = true;
    [Parameter] public string? SectionTitle { get; set; }

    private bool ShowAny =>
        LibraryProcessingSettingsDto.ShowIntroDetection(MediaType)
        || LibraryProcessingSettingsDto.ShowThemeSongGeneration(MediaType)
        || LibraryProcessingSettingsDto.ShowSeekbarThumbnails(MediaType)
        || LibraryProcessingSettingsDto.ShowChapterExtraction(MediaType)
        || LibraryProcessingSettingsDto.ShowMusicAudioAnalysis(MediaType)
        || LibraryProcessingSettingsDto.ShowTranscodeOptions(MediaType);

    private async Task OnIntroDetectionChangedAsync(bool enabled)
    {
        await IntroDetectionEnabledChanged.InvokeAsync(enabled);
        if (!enabled && ThemeSongGenerationEnabled)
            await ThemeSongGenerationEnabledChanged.InvokeAsync(false);
    }

    private Task OnThemeSongGenerationChangedAsync(bool enabled) =>
        ThemeSongGenerationEnabledChanged.InvokeAsync(enabled && IntroDetectionEnabled);
}
