using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Playlists;

public class SmartPlaylist : Playlist
{
    public MediaType? MediaType { get; set; }
    public SmartPlaylistMatchCondition MatchCondition { get; set; } = SmartPlaylistMatchCondition.All;
    public IList<SmartPlaylistRule> Rules { get; set; } = [];
    public int? Limit { get; set; }
    public SmartPlaylistOrderBy OrderBy { get; set; } = SmartPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; set; } = true;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
}
