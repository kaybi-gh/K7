using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Entities.Playlists;

public class SmartPlaylistRule
{
    public SmartPlaylistField Field { get; set; }
    public SmartPlaylistOperator Operator { get; set; }
    public string? Value { get; set; }
}
