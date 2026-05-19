using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Domain.Entities.Metadatas.External;
public class ExternalMovieMetadata : IExternalMetadata
{
    public string Title { get; init; } = string.Empty;
    public string? OriginalTitle { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public string? Overview { get; init; }
    public string? Tagline { get; init; }
    public string? OriginalLanguage { get; init; }
    public string? ContentRating { get; init; }
    public long? Budget { get; init; }
    public long? Revenue { get; init; }

    public IList<string> Genres { get; init; } = [];
    public IList<string> Studios { get; init; } = [];
    public IList<TrailerInfo> Trailers { get; init; } = [];
    public IList<string> RecommendedExternalIds { get; init; } = [];
    public IList<BasePersonRole> PersonRoles { get; init; } = [];
    public IList<ExternalId> ExternalIds { get; init; } = [];
    public IList<MetadataPicture> Pictures { get; init; } = [];
    public IList<MetadataProviderRating> Ratings { get; init; } = [];
}
