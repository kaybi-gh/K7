using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;

namespace K7.Server.Domain.Entities.Playlists;

public class DynamicPlaylist : Playlist
{
    public RuleGroup RuleFilter { get; set; } = new() { MatchCondition = RuleMatchCondition.All };
    public int? Limit { get; set; }
    public DynamicPlaylistOrderBy OrderBy { get; set; } = DynamicPlaylistOrderBy.DateAdded;
    public bool OrderDescending { get; set; } = true;
    public DateTimeOffset? LastEvaluatedAt { get; set; }
}
