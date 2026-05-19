using K7.Server.Domain.Entities.Medias;

namespace K7.Server.Domain.Entities;

public class MediaRecommendation
{
    public required Guid MediaId { get; set; }
    public BaseMedia Media { get; set; } = null!;

    public required string ProviderName { get; set; }
    public IList<string> RecommendedIds { get; set; } = [];
}
