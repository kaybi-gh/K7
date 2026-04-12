namespace K7.Shared.Dtos.Requests;

public sealed record MergeStrategy
{
    public PlayCountMergeMode PlayCount { get; init; } = PlayCountMergeMode.Additive;
    public RatingConflictMode Rating { get; init; } = RatingConflictMode.KeepExisting;
    public ProgressConflictMode Progress { get; init; } = ProgressConflictMode.MostRecent;
}

public enum PlayCountMergeMode
{
    Max,
    Additive
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
