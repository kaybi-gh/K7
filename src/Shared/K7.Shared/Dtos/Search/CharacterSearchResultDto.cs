using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Shared.Dtos.Search;

public sealed record CharacterSearchResultDto
{
    public Guid PersonRoleId { get; init; }
    public required string CharacterName { get; init; }
    public Guid PersonId { get; init; }
    public string? PersonName { get; init; }
    public MetadataPictureDto? PersonPortrait { get; init; }
    public Guid MediaId { get; init; }
    public string? MediaTitle { get; init; }
    public MediaType MediaType { get; init; }
}
