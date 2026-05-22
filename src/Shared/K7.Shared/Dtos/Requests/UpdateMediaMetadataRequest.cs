using K7.Shared.Dtos.Entities;

namespace K7.Shared.Dtos.Requests;

public sealed record UpdateMediaMetadataRequest
{
    public IList<string> LockedFields { get; init; } = [];
    public string? Title { get; init; }
    public string? OriginalTitle { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IList<string>? Genres { get; init; }
    public string? Tagline { get; init; }
    public string? Overview { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? ContentRating { get; init; }
    public long? Budget { get; init; }
    public long? Revenue { get; init; }
    public string? Status { get; init; }
    public string? Network { get; init; }
    public DateOnly? AirDate { get; init; }
    public int? Runtime { get; init; }
    public string? Biography { get; init; }
    public string? Country { get; init; }
    public int? TrackNumber { get; init; }
    public int? DiscNumber { get; init; }
    public string? Lyrics { get; init; }
    public string? LyricsLrc { get; init; }
    public IList<ExternalIdEditDto>? ExternalIds { get; init; }
}
