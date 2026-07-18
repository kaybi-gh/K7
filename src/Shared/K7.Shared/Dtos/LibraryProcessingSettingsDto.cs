using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record LibraryProcessingSettingsDto
{
    public bool IntroDetectionEnabled { get; init; } = true;
    public bool SeekbarThumbnailGenerationEnabled { get; init; } = true;
    public bool ChapterExtractionEnabled { get; init; } = true;
    public bool MusicAudioAnalysisEnabled { get; init; } = true;
    public bool TranscodingEnabled { get; init; } = true;
    public bool TransmuxingEnabled { get; init; } = true;

    public static LibraryProcessingSettingsDto Defaults => new();

    public static bool ShowIntroDetection(LibraryMediaType mediaType) =>
        mediaType is LibraryMediaType.Serie;

    public static bool ShowSeekbarThumbnails(LibraryMediaType mediaType) =>
        mediaType is LibraryMediaType.Movie or LibraryMediaType.Serie;

    public static bool ShowChapterExtraction(LibraryMediaType mediaType) =>
        mediaType is LibraryMediaType.Movie or LibraryMediaType.Serie;

    public static bool ShowMusicAudioAnalysis(LibraryMediaType mediaType) =>
        mediaType is LibraryMediaType.Music;

    public static bool ShowTranscodeOptions(LibraryMediaType mediaType) =>
        mediaType is LibraryMediaType.Movie or LibraryMediaType.Serie;
}
