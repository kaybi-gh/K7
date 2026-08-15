using K7.Import.Matching;

namespace K7.Import.Models;

public enum MatchStatus
{
    MatchedByExternalId,
    MatchedByPath,
    MatchedByTitle,
    Created,
    WouldCreate,
    Unmatched
}

public enum UserMappingKind
{
    MappedExisting,
    AutoMapped,
    ReuseTemp,
    WouldCreateTemp,
    CreatedTemp,
    Skipped
}

public sealed record ItemMatchResult
{
    public required SourceMediaItem Item { get; init; }
    public required MatchStatus Status { get; init; }
    public Guid? MediaId { get; init; }
}

public sealed record MatchOutcome
{
    public required Dictionary<string, Guid> Matches { get; init; }
    public required IReadOnlyList<ItemMatchResult> ItemResults { get; init; }
    public int CreatedCount { get; init; }
    public int WouldCreateCount { get; init; }
    public int UnmatchedCount { get; init; }

    public IEnumerable<ItemMatchResult> MatchedExisting =>
        ItemResults.Where(r => r.Status is MatchStatus.MatchedByExternalId
            or MatchStatus.MatchedByPath
            or MatchStatus.MatchedByTitle);

    public IEnumerable<ItemMatchResult> WouldCreateItems =>
        ItemResults.Where(r => r.Status is MatchStatus.WouldCreate);

    public IEnumerable<ItemMatchResult> CreatedItems =>
        ItemResults.Where(r => r.Status is MatchStatus.Created);

    public IEnumerable<ItemMatchResult> UnmatchedItems =>
        ItemResults.Where(r => r.Status is MatchStatus.Unmatched);
}

public sealed record UserPlan
{
    public required SourceUser Source { get; init; }
    public required Guid K7UserId { get; init; }
    public required string TargetUsername { get; init; }
    public required UserMappingKind Kind { get; init; }
    public string? SkipReason { get; init; }
}

public sealed record PlaylistPreview
{
    public required string Title { get; init; }
    public bool IsDynamic { get; init; }
    public bool Skipped { get; init; }
    public string? SkipReason { get; init; }
    public int SourceItems { get; init; }
    public int Matched { get; init; }
    public int WouldCreate { get; init; }
    public int Created { get; init; }
    public int Unmatched { get; init; }
}

public sealed record UserImportPreview
{
    public required UserPlan Plan { get; init; }

    public int HistorySourceItems { get; set; }
    public int HistoryMatched { get; set; }
    public int HistoryWouldCreate { get; set; }
    public int HistoryCreated { get; set; }
    public int HistoryUnmatched { get; set; }
    public int WatchStates { get; set; }
    public int PlaybackSessions { get; set; }

    public int RatingsSourceItems { get; set; }
    public int RatingsMatched { get; set; }
    public int RatingsWouldCreate { get; set; }
    public int RatingsCreated { get; set; }
    public int RatingsUnmatched { get; set; }
    public int RatingsToImport { get; set; }

    public int SkippedDynamicPlaylists { get; set; }
    public List<PlaylistPreview> Playlists { get; set; } = [];
}

public sealed class ImportReport
{
    public bool DryRun { get; init; }
    public List<UserPlan> Users { get; } = [];
    public List<UserImportPreview> PerUser { get; } = [];
    public Dictionary<string, ItemMatchResult> MediaBySourceId { get; } = new(StringComparer.Ordinal);

    public int MatchedByExternalId => CountStatus(MatchStatus.MatchedByExternalId);
    public int MatchedByPath => CountStatus(MatchStatus.MatchedByPath);
    public int MatchedByTitle => CountStatus(MatchStatus.MatchedByTitle);
    public int MatchedExisting => MatchedByExternalId + MatchedByPath + MatchedByTitle;
    public int CreatedMedias => CountCreates(MatchStatus.Created);
    public int WouldCreateMedias => CountCreates(MatchStatus.WouldCreate);
    public int UnmatchedMedias => CountStatus(MatchStatus.Unmatched);

    public void MergeMedia(MatchOutcome outcome)
    {
        foreach (var result in outcome.ItemResults)
            MediaBySourceId[result.Item.Id] = result;
    }

    private int CountStatus(MatchStatus status) =>
        MediaBySourceId.Values.Count(r => r.Status == status);

    private int CountCreates(MatchStatus status) =>
        MusicItemCollapser.DistinctCreates(MediaBySourceId.Values.Where(r => r.Status == status)).Count;
}
