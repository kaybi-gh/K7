using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Domain.Entities.Metadatas.External;

public class ExternalEpisodeMetadata : IExternalMetadata
{
    public Guid? RemoteId { get; init; }
    public int EpisodeNumber { get; init; }
    public int SeasonNumber { get; init; }
    public string? Title { get; init; }
    public string? SortTitle { get; init; }
    public string? Overview { get; init; }
    public DateOnly? AirDate { get; init; }
    public int? Runtime { get; init; }
    public string? StillImageUrl { get; init; }

    public IList<ExternalId> ExternalIds { get; init; } = [];
    public IList<BasePersonRole> PersonRoles { get; init; } = [];
    public IList<MetadataProviderRating> Ratings { get; init; } = [];
}
