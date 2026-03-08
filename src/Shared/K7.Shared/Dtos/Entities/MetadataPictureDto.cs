using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos.Entities;

public sealed record MetadataPictureDto
{
    public Guid Id { get; init; }
    public MetadataPictureType Type { get; init; }
    public Uri? Uri { get; init; }
    public IEnumerable<MetadataPictureSize> AvailableSizes { get; init; } = [];

    /// <summary>
    /// Returns the URI for a specific size variant. Falls back to the original if size is null.
    /// </summary>
    public Uri? GetUri(MetadataPictureSize? size = null)
    {
        if (Uri is null) return null;
        if (size is null) return Uri;
        return new Uri($"{Uri.OriginalString}?size={size}", UriKind.Relative);
    }

    public static MetadataPictureDto FromDomain(MetadataPicture domain) => new()
    {
        Id = domain.Id,
        Type = domain.Type,
        Uri = new Uri($"/api/metadata-pictures/{domain.Id}", UriKind.Relative),
        AvailableSizes = domain.Variants.Select(v => v.Size)
    };
}
