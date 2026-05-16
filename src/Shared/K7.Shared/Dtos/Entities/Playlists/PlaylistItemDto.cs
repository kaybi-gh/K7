namespace K7.Shared.Dtos.Entities.Playlists;

public sealed record PlaylistItemDto
{
    public Guid Id { get; init; }
    public Guid MediaId { get; init; }
    public int Order { get; init; }
    public string? MediaTitle { get; init; }
    public string? ArtistName { get; init; }
    public Guid? ArtistId { get; init; }
    public string? AlbumTitle { get; init; }
    public string? Genre { get; init; }
    public Guid? IndexedFileId { get; init; }
    public double? Duration { get; init; }
    public int? UserRating { get; init; }
    public double? Bpm { get; init; }
    public string? MusicalKey { get; init; }
    public double? Energy { get; init; }
    public IReadOnlyList<MetadataPictureDto>? Pictures { get; init; }

}
