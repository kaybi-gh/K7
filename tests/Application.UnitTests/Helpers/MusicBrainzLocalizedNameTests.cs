using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

public class MusicBrainzLocalizedNameTests
{
    [Test]
    public void Resolve_ShouldPreferEnglishAlias_WhenLibraryLanguageIsFrenchAndOfficialIsJapanese()
    {
        var result = MusicBrainzLocalizedName.Resolve(
            "近藤浩治",
            "Kondo, Koji",
            [
                new MusicBrainzNameAlias("Koji Kondo", "en", IsPrimary: true, "Artist name", "Kondo, Koji"),
                new MusicBrainzNameAlias("近藤浩治", "ja", IsPrimary: true, "Artist name", "コンドウ コウジ"),
                new MusicBrainzNameAlias("Zelda", null, IsPrimary: false, "Search hint", "Zelda")
            ],
            "fr",
            unfoldPersonSortName: true);

        result.Name.Should().Be("Koji Kondo");
        result.OriginalName.Should().Be("近藤浩治");
        result.SortName.Should().Be("Kondo, Koji");
    }

    [Test]
    public void Resolve_ShouldKeepOfficialJapanese_WhenLibraryLanguageIsJapanese()
    {
        var result = MusicBrainzLocalizedName.Resolve(
            "近藤浩治",
            "Kondo, Koji",
            [
                new MusicBrainzNameAlias("Koji Kondo", "en", IsPrimary: true, "Artist name", "Kondo, Koji"),
                new MusicBrainzNameAlias("近藤浩治", "ja", IsPrimary: true, "Artist name", "コンドウ コウジ")
            ],
            "ja");

        result.Name.Should().Be("近藤浩治");
        result.OriginalName.Should().BeNull();
    }

    [Test]
    public void Resolve_ShouldKeepLatinOfficialName_WhenNoLocaleAliasExists()
    {
        var result = MusicBrainzLocalizedName.Resolve(
            "Daft Punk",
            "Daft Punk",
            [],
            "fr");

        result.Name.Should().Be("Daft Punk");
        result.OriginalName.Should().BeNull();
    }

    [Test]
    public void Resolve_ShouldUnfoldLatinSortName_WhenArtistHasNoAliases()
    {
        var result = MusicBrainzLocalizedName.Resolve(
            "近藤浩治",
            "Kondo, Koji",
            [],
            "fr",
            unfoldPersonSortName: true);

        result.Name.Should().Be("Koji Kondo");
        result.OriginalName.Should().Be("近藤浩治");
    }

    [Test]
    public void Resolve_ShouldPreferEnglishReleaseGroupAlias_WhenOfficialAlbumTitleIsJapanese()
    {
        var result = MusicBrainzLocalizedName.Resolve(
            "ゼルダの伝説 時のオカリナ オリジナルサウンドトラック",
            null,
            [
                new MusicBrainzNameAlias(
                    "The Legend of Zelda: Ocarina of Time: Official Soundtrack",
                    "en",
                    IsPrimary: true,
                    "Release group name",
                    "Legend of Zelda, The: Ocarina of Time: Official Soundtrack"),
                new MusicBrainzNameAlias(
                    "ゼルダの伝説 時のオカリナ オリジナルサウンドトラック",
                    "ja",
                    IsPrimary: true,
                    "Release group name",
                    null)
            ],
            "fr");

        result.Name.Should().Be("The Legend of Zelda: Ocarina of Time: Official Soundtrack");
        result.OriginalName.Should().Be("ゼルダの伝説 時のオカリナ オリジナルサウンドトラック");
    }
}
