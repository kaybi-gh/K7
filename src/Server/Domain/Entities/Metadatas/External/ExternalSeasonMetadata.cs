using K7.Server.Domain.Interfaces;

namespace K7.Server.Domain.Entities.Metadatas.External;

public class ExternalSeasonMetadata : IExternalMetadata
{
    public int SeasonNumber { get; init; }
    public string? Title { get; init; }
    public string? Overview { get; init; }
    public DateOnly? AirDate { get; init; }
    public int? EpisodeCount { get; init; }

    public IList<ExternalId> ExternalIds { get; init; } = [];
    public IList<MetadataPicture> Pictures { get; init; } = [];
}
