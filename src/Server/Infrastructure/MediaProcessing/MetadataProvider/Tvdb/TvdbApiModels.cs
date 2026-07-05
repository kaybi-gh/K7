using System.Text.Json.Serialization;

namespace K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

public sealed class TvdbApiResponse<T>
{
    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("data")]
    public T? Data { get; init; }
}

public sealed class TvdbLoginResponse
{
    [JsonPropertyName("token")]
    public string? Token { get; init; }
}

public sealed class TvdbSearchResult
{
    [JsonPropertyName("objectID")]
    public string? ObjectId { get; init; }

    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("tvdb_id")]
    public string? TvdbId { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("name_translated")]
    public string? NameTranslated { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; init; }

    [JsonPropertyName("first_air_time")]
    public string? FirstAirTime { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }
}

public sealed class TvdbSeriesExtended
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("slug")]
    public string? Slug { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("firstAired")]
    public string? FirstAired { get; init; }

    [JsonPropertyName("lastAired")]
    public string? LastAired { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("originalLanguage")]
    public string? OriginalLanguage { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("score")]
    public double? Score { get; init; }

    [JsonPropertyName("status")]
    public TvdbStatus? Status { get; init; }

    [JsonPropertyName("genres")]
    public List<TvdbGenre>? Genres { get; init; }

    [JsonPropertyName("seasons")]
    public List<TvdbSeasonBase>? Seasons { get; init; }

    [JsonPropertyName("artworks")]
    public List<TvdbArtwork>? Artworks { get; init; }

    [JsonPropertyName("characters")]
    public List<TvdbCharacter>? Characters { get; init; }

    [JsonPropertyName("companies")]
    public List<TvdbCompany>? Companies { get; init; }

    [JsonPropertyName("originalNetwork")]
    public TvdbCompany? OriginalNetwork { get; init; }

    [JsonPropertyName("latestNetwork")]
    public TvdbCompany? LatestNetwork { get; init; }

    [JsonPropertyName("contentRatings")]
    public List<TvdbContentRating>? ContentRatings { get; init; }

    [JsonPropertyName("remoteIds")]
    public List<TvdbRemoteId>? RemoteIds { get; init; }

    [JsonPropertyName("trailers")]
    public List<TvdbTrailer>? Trailers { get; init; }
}

public sealed class TvdbStatus
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed class TvdbGenre
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed class TvdbSeasonBase
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }
}

public sealed class TvdbSeasonExtended
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("year")]
    public string? Year { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("artwork")]
    public List<TvdbArtwork>? Artwork { get; init; }

    [JsonPropertyName("episodes")]
    public List<TvdbEpisodeBase>? Episodes { get; init; }
}

public sealed class TvdbSeriesEpisodesPage
{
    [JsonPropertyName("episodes")]
    public List<TvdbEpisodeBase>? Episodes { get; init; }
}

public sealed class TvdbEpisodeBase
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("seriesId")]
    public int SeriesId { get; init; }

    [JsonPropertyName("seasonNumber")]
    public int SeasonNumber { get; init; }

    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("absoluteNumber")]
    public int? AbsoluteNumber { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("aired")]
    public string? Aired { get; init; }

    [JsonPropertyName("runtime")]
    public int? Runtime { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }
}

public sealed class TvdbEpisodeExtended
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("seriesId")]
    public int SeriesId { get; init; }

    [JsonPropertyName("seasonNumber")]
    public int SeasonNumber { get; init; }

    [JsonPropertyName("number")]
    public int Number { get; init; }

    [JsonPropertyName("absoluteNumber")]
    public int? AbsoluteNumber { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("aired")]
    public string? Aired { get; init; }

    [JsonPropertyName("runtime")]
    public int? Runtime { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("characters")]
    public List<TvdbCharacter>? Characters { get; init; }

    [JsonPropertyName("remoteIds")]
    public List<TvdbRemoteId>? RemoteIds { get; init; }

    [JsonPropertyName("artworks")]
    public List<TvdbArtwork>? Artworks { get; init; }
}

public sealed class TvdbArtwork
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("thumbnail")]
    public string? Thumbnail { get; init; }

    [JsonPropertyName("type")]
    public int Type { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("score")]
    public double? Score { get; init; }

    [JsonPropertyName("width")]
    public long? Width { get; init; }

    [JsonPropertyName("height")]
    public long? Height { get; init; }
}

public sealed class TvdbCharacter
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("peopleId")]
    public int? PeopleId { get; init; }

    [JsonPropertyName("peopleType")]
    public string? PeopleType { get; init; }

    [JsonPropertyName("personName")]
    public string? PersonName { get; init; }

    [JsonPropertyName("personImgURL")]
    public string? PersonImgUrl { get; init; }

    [JsonPropertyName("image")]
    public string? Image { get; init; }

    [JsonPropertyName("sort")]
    public long Sort { get; init; }
}

public sealed class TvdbCompany
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("companyType")]
    public TvdbCompanyType? CompanyType { get; init; }
}

public sealed class TvdbCompanyType
{
    [JsonPropertyName("companyTypeName")]
    public string? CompanyTypeName { get; init; }
}

public sealed class TvdbContentRating
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("country")]
    public string? Country { get; init; }
}

public sealed class TvdbRemoteId
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("sourceName")]
    public string? SourceName { get; init; }

    [JsonPropertyName("type")]
    public int? Type { get; init; }
}

public sealed class TvdbTrailer
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }
}

public sealed class TvdbTranslation
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("overview")]
    public string? Overview { get; init; }

    [JsonPropertyName("language")]
    public string? Language { get; init; }
}

public sealed class TvdbSearchByRemoteIdResult
{
    [JsonPropertyName("series")]
    public TvdbSeriesBase? Series { get; init; }
}

public sealed class TvdbSeriesBase
{
    [JsonPropertyName("id")]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}
