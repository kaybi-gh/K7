namespace K7.Import.Models;

// Kept for compatibility with older call sites; prefer ImportReport for new reporting.
public sealed record ImportResult
{
    public int MatchedItems { get; set; }
    public int MatchedByExternalId { get; set; }
    public int MatchedByPath { get; set; }
    public int MatchedExistingByTitle { get; set; }
    public int UnmatchedItems { get; set; }
    public int CreatedMedias { get; set; }
    public int WouldCreateMedias { get; set; }
    public int ImportedWatchStates { get; set; }
    public int ImportedPlaybackSessions { get; set; }
    public int ImportedRatings { get; set; }
    public int ImportedPlaylists { get; set; }
    public int WouldImportWatchStates { get; set; }
    public int WouldImportPlaybackSessions { get; set; }
    public int WouldImportRatings { get; set; }
    public int WouldImportPlaylists { get; set; }
    public int SkippedDynamicPlaylists { get; set; }
    public List<string> UnmatchedTitles { get; set; } = [];
    public List<string> WouldCreateTitles { get; set; } = [];
}
