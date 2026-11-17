namespace K7.Shared.Enums;

/// <summary>
/// The kinds of media pages that can have their own settings instead of the shared ones.
/// Stored in preferences as a key: only add values at the end.
/// </summary>
public enum MediaPageKind
{
    Movie,
    Series,
    Episode,
    Person,

    /// <summary>
    /// The page of a season. It has no hero of the same kind: its one setting is
    /// <see cref="SeasonPageLayout"/>, not a set of media page settings of its own.
    /// </summary>
    Season
}
