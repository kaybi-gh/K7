using K7.Server.Domain.Enums;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Domain.Entities.Playlists;

public class SmartPlaylist : Playlist
{
    public RuleGroup RuleFilter { get; set; } = new() { MatchCondition = RuleMatchCondition.All };
    public int? Limit { get; set; }
    public SmartPlaylistOrderBy OrderBy { get; set; } = SmartPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; set; } = true;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
}
