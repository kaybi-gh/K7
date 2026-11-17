using K7.Shared.Enums;

namespace K7.Shared.Theming;

/// <summary>
/// How a media page shows: its hero, its poster and its lists. Either the settings shared by
/// every media page, or the ones a kind of page (movies, series) has of its own.
/// </summary>
public sealed record MediaPageSkinDto
{
    public MediaHeroStyle Hero { get; set; }
    public MediaHeroSize HeroSize { get; set; }
    public bool Poster { get; set; }
    public MediaListStyle Lists { get; set; }
    public bool GridMore { get; set; }
}
