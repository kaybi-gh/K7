namespace K7.Server.Domain.Models;

/// <summary>
/// Optional hints for picking a MusicBrainz release inside a release-group.
/// </summary>
public sealed record MusicAlbumReleaseHints
{
    public int? ExpectedTrackCount { get; init; }
    public IReadOnlyList<string>? ExpectedTrackTitles { get; init; }
    public TimeSpan? ExpectedTotalDuration { get; init; }
    public string? PreferredReleaseId { get; init; }
}
