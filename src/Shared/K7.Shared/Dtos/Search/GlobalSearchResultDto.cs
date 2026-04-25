using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;

namespace K7.Shared.Dtos.Search;

public sealed record GlobalSearchResultDto
{
    public IReadOnlyList<LiteMediaDto> MediaResults { get; init; } = [];
    public IReadOnlyList<LitePersonDto> PersonResults { get; init; } = [];
    public IReadOnlyList<CharacterSearchResultDto> CharacterResults { get; init; } = [];
}
