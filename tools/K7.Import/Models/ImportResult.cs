namespace K7.Import.Models;

public sealed record ImportResult
{
    public int MatchedItems { get; set; }
    public int MatchedByExternalId { get; set; }
    public int MatchedByPath { get; set; }
    public int MatchedExistingByTitle { get; set; }
    public int UnmatchedItems { get; set; }
    public int CreatedMedias { get; set; }
    public int ImportedWatchStates { get; set; }
    public int ImportedPlaybackSessions { get; set; }
    public int ImportedRatings { get; set; }
    public int ImportedPlaylists { get; set; }
    public int SkippedSmartPlaylists { get; set; }
    public List<string> UnmatchedTitles { get; set; } = [];
}
