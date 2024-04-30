using System.Text.Json.Serialization;

namespace MediaClient.Shared.Domain.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MovieDto), "Movie")]
public abstract record MediaDto
{
    public Guid Id { get; init; }
    public string Slug { get; init; } = null!;
    public string? Title { get; init; }
    public DateOnly? ReleaseDate { get; init; }
    public IEnumerable<MetadataPictureDto>? Pictures { get; init; }
    public IEnumerable<PersonRoleDto>? PersonRoles { get; init; }
    public IEnumerable<RatingDto>? Ratings { get; init; }
    public IEnumerable<IndexedFileDto>? IndexedFiles { get; init; }
}

public record MetadataPictureDto
{
    public Guid Id { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MetadataPictureType? Type { get; init; }
    public Uri? Uri { get; init; }
}

public record RatingDto
{
    public Guid Id { get; init; }
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RatingSource? Source { get; init; }
    public double? Value { get; init; }
    public double? MinimumValue { get; init; }
    public double? MaximumValue { get; init; }
}

public record MovieDto : MediaDto
{
    public string? TagLine { get; init; }
    public string? Overview { get; init; }
    public string? OriginalLanguage { get; init; }
}

public enum MediaType
{
    Movie = 1,
    MusicAlbum = 2,
    MusicTrack = 3,
    Serie = 4,
    SerieEpisode = 5,
    SerieSeason = 6
}

public enum RatingSource
{
    MetadataProvider = 1,
    LocalUser = 2
}

public enum MetadataPictureType
{
    Poster = 1,
    Backdrop = 2,
    Thumbnail = 3,
    Logo = 4,
    Portrait = 5
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ActorDto), "Actor")]
public abstract record PersonRoleDto
{
    public Guid Id { get; init; }
    public Guid PersonId { get; init; }
    public string PersonSlug { get; init; } = null!;
    public string PersonName { get; init; } = null!;
    public MetadataPictureDto? PortraitPicture { get; init; }
}

public record ActorDto : PersonRoleDto
{
    public string? CharacterName { get; init; }
}

public record IndexedFileDto
{
    public required Guid LibraryId { get; init; }
    public required string Name { get; init; }
    public required string Extension { get; init; }
    public required string Path { get; init; }
    public string? ParentDirectory { get; init; }
    public required string Hash { get; init; }
    public required long Size { get; init; }
    public bool IsSplitPart { get; init; }
    public bool IsComposite { get; init; }
}
