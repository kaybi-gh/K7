using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record SmartPlaylistRuleDto
{
    public SmartPlaylistField Field { get; init; }
    public SmartPlaylistOperator Operator { get; init; }
    public string? Value { get; init; }
}
