namespace K7.Shared.Dtos.Requests;

public sealed record MergeStrategy
{
    public PlayCountMergeMode PlayCount { get; init; } = PlayCountMergeMode.Additive;
    public RatingConflictMode Rating { get; init; } = RatingConflictMode.KeepExisting;
    public ProgressConflictMode Progress { get; init; } = ProgressConflictMode.MostRecent;
    public PlaylistMergeMode Playlist { get; init; } = PlaylistMergeMode.Transfer;
}

public enum PlayCountMergeMode
{
    Max,
    Additive,
    Ignore
}

public enum RatingConflictMode
{
    Overwrite,
    KeepExisting
}

public enum ProgressConflictMode
{
    MostRecent,
    AlwaysOverwrite
}

public enum PlaylistMergeMode
{
    Transfer,
    Delete
}
