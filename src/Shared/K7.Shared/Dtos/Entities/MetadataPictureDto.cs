using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record MetadataPictureDto
{
    public Guid Id { get; init; }
    public MetadataPictureType Type { get; init; }
    public Uri? Uri { get; init; }

    public static MetadataPictureDto FromDomain(MetadataPicture domain) => new()
    {
        Id = domain.Id,
        Type = domain.Type,
        Uri = new Uri($"/api/metadata-pictures/{domain.Id}", UriKind.Relative)
    };
}
