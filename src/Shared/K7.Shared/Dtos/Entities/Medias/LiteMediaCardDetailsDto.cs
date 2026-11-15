namespace K7.Shared.Dtos.Entities.Medias;

/// <summary>
/// Details a media card can show beyond the title and the year. Only sent when a browse query
/// asks for them (the card style of the user shows at least one).
/// </summary>
public sealed record LiteMediaCardDetailsDto
{
    /// <summary>First genre of the media.</summary>
    public string? Genre { get; init; }

    public string? ContentRating { get; init; }

    public int? RuntimeMinutes { get; init; }

    /// <summary>Rating of the metadata provider, on 10.</summary>
    public double? Rating { get; init; }

    public bool IsEmpty => Genre is null && ContentRating is null && RuntimeMinutes is null && Rating is null;
}
