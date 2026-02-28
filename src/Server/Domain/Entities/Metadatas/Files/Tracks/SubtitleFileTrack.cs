namespace K7.Server.Domain.Entities.Metadatas.Files.Tracks;

public class SubtitleFileTrack() : BaseFileTrack(FileTrackType.Subtitle)
{
    public Guid? VideoFileMetadataId { get; set; }
    public string? Name { get; set; }
    public string? Language { get; set; }
    public required string Codec { get; set; }

    /// <summary>
    /// True for text-based subtitles (SRT, ASS, SSA, VTT, SubRip) that can be served as WebVTT.
    /// False for bitmap-based subtitles (PGS, DVBSUB) that require burn-in.
    /// </summary>
    public bool IsTextBased { get; set; }

    /// <summary>
    /// Indicates a forced subtitle track (e.g. foreign language dialogue only).
    /// </summary>
    public bool IsForced { get; set; }
}
