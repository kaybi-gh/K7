namespace K7.Shared.Dtos.Diagnostics;

public static class LibraryHealthSummaryCounts
{
    public static int SumErrors(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.OrphanIndexedFileCount + l.MediaWithoutFilesCount + l.MissingFileMetadataCount);

    public static int SumWarnings(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.UnidentifiedIndexedFileCount + l.MissingHlsSegmentsCount
            + l.MediaMissingPicturesCount + l.MediaMissingMetadataCount + l.StaleMetadataCount + l.InaccessiblePathCount);

    public static int SumInfo(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        summaries.Sum(l => l.MissingAudioAnalysisCount);

    public static int SumTotal(IEnumerable<LibraryHealthSummaryDto> summaries) =>
        SumErrors(summaries) + SumWarnings(summaries) + SumInfo(summaries);
}
