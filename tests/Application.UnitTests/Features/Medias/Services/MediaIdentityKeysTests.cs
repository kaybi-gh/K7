using FluentAssertions;
using K7.Server.Application.Features.Medias.Services;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

public class MediaIdentityKeysTests
{
    [Test]
    public void ResolveMusicTitleAndArtist_ShouldSplitTitleDashArtist_WhenArtistIsMissing()
    {
        var (title, artist) = MediaIdentityKeys.ResolveMusicTitleAndArtist("Efile - KIZ", artistName: null);

        title.Should().Be("Efile");
        artist.Should().Be("KIZ");
    }

    [Test]
    public void ResolveMusicTitleAndArtist_ShouldKeepTitle_WhenArtistIsPresent()
    {
        var (title, artist) = MediaIdentityKeys.ResolveMusicTitleAndArtist("Efile", "KIZ");

        title.Should().Be("Efile");
        artist.Should().Be("KIZ");
    }

    [Test]
    public void ResolveMusicTitleAndArtist_ShouldStripArtistPrefix_WhenArtistIsAlreadyKnown()
    {
        var (title, artist) = MediaIdentityKeys.ResolveMusicTitleAndArtist("KIZ - Efile", "KIZ");

        title.Should().Be("Efile");
        artist.Should().Be("KIZ");
    }

    [Test]
    public void MatchesIgnoringDiacritics_ShouldAlignHyphenAndSpace()
    {
        MediaIdentityKeys.MatchesIgnoringDiacritics("Cerf-volant", "Cerf volant").Should().BeTrue();
    }

    [Test]
    public void StripTrackEditionSuffix_ShouldDropOriginalVersion_ButKeepLive()
    {
        MediaIdentityKeys.StripTrackEditionSuffix("Ain't No Rest For The Wicked - Original Version")
            .Should().Be("Ain't No Rest For The Wicked");
        MediaIdentityKeys.StripTrackEditionSuffix("Ain't No Rest For The Wicked (Original Version)")
            .Should().Be("Ain't No Rest For The Wicked");
        MediaIdentityKeys.StripTrackEditionSuffix("Ain't No Rest for the Wicked (Live)")
            .Should().Be("Ain't No Rest for the Wicked (Live)");
    }

    [Test]
    public void NormalizeMusicTitle_ShouldAlignOriginalVersionWithCoreTitle()
    {
        var core = MediaIdentityKeys.NormalizeMusicTitle("Cage The Elephant", "Ain't No Rest for the Wicked");
        var original = MediaIdentityKeys.NormalizeMusicTitle(
            "Cage The Elephant", "Ain't No Rest For The Wicked - Original Version");

        original.Equals(core, StringComparison.OrdinalIgnoreCase).Should().BeTrue();
    }

    [Test]
    public void TitleLookupVariants_ShouldIncludeHyphenAndCurlyApostropheTwins()
    {
        MediaIdentityKeys.TitleLookupVariants("Cerf volant").Should().Contain("Cerf-volant");
        MediaIdentityKeys.TitleLookupVariants("Ain't No Rest for the Wicked")
            .Should().Contain("Ain\u2019t No Rest for the Wicked");
    }

    [Test]
    public void IsVariousArtist_ShouldDetectCompilationArtists()
    {
        MediaIdentityKeys.IsVariousArtist("Various Artists").Should().BeTrue();
        MediaIdentityKeys.IsVariousArtist("Artistes divers").Should().BeTrue();
        MediaIdentityKeys.IsVariousArtist("KIZ").Should().BeFalse();
    }

    [Test]
    public void YearsCompatible_ShouldAllowOneYearSlack()
    {
        MediaIdentityKeys.YearsCompatible(2007, new DateOnly(2006, 12, 9)).Should().BeTrue();
        MediaIdentityKeys.YearsCompatible(2018, new DateOnly(1954, 1, 1)).Should().BeFalse();
        MediaIdentityKeys.YearsCompatible(null, new DateOnly(2006, 12, 9)).Should().BeTrue();
    }

    [Test]
    public void NormalizeMusicTitle_ShouldAlignPlexAndJellyfinShapes()
    {
        var fromJellyfin = MediaIdentityKeys.NormalizeMusicTitle("KIZ", "Efile");
        var fromPlex = MediaIdentityKeys.NormalizeMusicTitle(null, "Efile - KIZ");

        fromJellyfin.Should().Be("KIZ - Efile");
        fromPlex.Should().Be(fromJellyfin);
    }

    [Test]
    public void StripAlbumEditionSuffix_ShouldDropDeluxe_ButKeepLive()
    {
        MediaIdentityKeys.StripAlbumEditionSuffix("Des tours (Deluxe)").Should().Be("Des tours");
        MediaIdentityKeys.StripAlbumEditionSuffix("Des tours (Deluxe Edition)").Should().Be("Des tours");
        MediaIdentityKeys.StripAlbumEditionSuffix("Aladdin (Original Motion Picture Soundtrack)").Should().Be("Aladdin");
        MediaIdentityKeys.StripAlbumEditionSuffix("Des tours").Should().Be("Des tours");
        MediaIdentityKeys.StripAlbumEditionSuffix("Des tours (Live)").Should().Be("Des tours (Live)");
    }

    [Test]
    public void SeriesTitleLookupVariants_ShouldDropCountrySuffix()
    {
        var variants = MediaIdentityKeys.SeriesTitleLookupVariants("The Office (US)");

        variants.Should().Contain("The Office (US)");
        variants.Should().Contain("The Office");
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldAlignUsSuffixWithBareTitle()
    {
        MediaIdentityKeys.SeriesTitlesOverlap("The Office (US)", "The Office").Should().BeTrue();
        MediaIdentityKeys.SeriesTitlesOverlap("The Office", "The Office (UK)").Should().BeTrue();
        MediaIdentityKeys.SeriesTitlesOverlap("The Office", "Parks and Recreation").Should().BeFalse();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldAlignColonSpacing()
    {
        MediaIdentityKeys.SeriesTitlesOverlap(
                "Demon Slayer : Kimetsu no Yaiba",
                "Demon Slayer: Kimetsu no Yaiba")
            .Should().BeTrue();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldAlignRankingOfKingsCourageSpinOff()
    {
        MediaIdentityKeys.SeriesTitlesOverlap(
                "Ranking of Kings : Le tr\u00e9sor du courage",
                "Ranking of Kings : Le tr\u00e9sor du courage")
            .Should().BeTrue();
        MediaIdentityKeys.SeriesTitlesOverlap(
                "Ranking of Kings : Le tr\u00e9sor du courage",
                "Ranking of Kings")
            .Should().BeFalse();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldAlignCommaWithColonSubtitle()
    {
        MediaIdentityKeys.SeriesTitlesOverlap(
                "90210 Beverly Hills : Nouvelle generation",
                "90210 Beverly Hills, Nouvelle Generation")
            .Should().BeTrue();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldNotStripYear_ByDefault()
    {
        MediaIdentityKeys.SeriesTitlesOverlap("One Piece", "One Piece (2023)").Should().BeFalse();
        MediaIdentityKeys.SeriesTitlesOverlap("Hunter x Hunter", "Hunter x Hunter (2011)").Should().BeFalse();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldAlignFaceToFaceWithAccentAndYear()
    {
        MediaIdentityKeys.SeriesTitlesOverlap(
                "Face to Face",
                "Face \u00e0 face (2022)",
                includeYearSuffix: true)
            .Should().BeTrue();
        MediaIdentityKeys.SeriesTitlesOverlap("Face to Face", "Face \u00e0 face (2022)")
            .Should().BeFalse();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldStripYear_WhenRequested()
    {
        MediaIdentityKeys.SeriesTitlesOverlap("Hunter x Hunter", "Hunter x Hunter (2011)", includeYearSuffix: true)
            .Should().BeTrue();
    }

    [Test]
    public void ResolveSeriesMatches_ShouldPreferExactTitle_WhenYearSuffixedSiblingExists()
    {
        var series = new (string Title, string? Original)[]
        {
            ("One Piece", null),
            ("One Piece (2023)", null)
        };

        var matches = MediaIdentityKeys.ResolveSeriesMatches(
            "One Piece",
            series,
            s => s.Title,
            s => s.Original);

        matches.Should().ContainSingle().Which.Title.Should().Be("One Piece");
    }

    [Test]
    public void ResolveSeriesMatches_ShouldUseYearSuffix_WhenOnlyOneCandidateRemains()
    {
        var series = new (string Title, string? Original)[]
        {
            ("Hunter x Hunter (2011)", null),
            ("JoJo's Bizarre Adventure (2012)", null)
        };

        var matches = MediaIdentityKeys.ResolveSeriesMatches(
            "Hunter x Hunter",
            series,
            s => s.Title,
            s => s.Original);

        matches.Should().ContainSingle().Which.Title.Should().Be("Hunter x Hunter (2011)");
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldAlignFractionSlashAndAsciiSlash()
    {
        MediaIdentityKeys.SeriesTitlesOverlap("Fate/Extra: Last Encore", "Fate\u2044Extra: Last Encore")
            .Should().BeTrue();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldIgnoreTrailingPeriod()
    {
        MediaIdentityKeys.SeriesTitlesOverlap("Bref", "bref.").Should().BeTrue();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldIgnorePresentsFiller()
    {
        MediaIdentityKeys.SeriesTitlesOverlap("The Boys: Diabolical", "The Boys Presents: Diabolical")
            .Should().BeTrue();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldFoldBungouRomanization()
    {
        MediaIdentityKeys.SeriesTitlesOverlap("Bungou Stray Dogs", "Bungo Stray Dogs").Should().BeTrue();
    }

    [Test]
    public void SeriesShortName_ShouldTakeTextBeforeSeparator()
    {
        MediaIdentityKeys.SeriesShortName("Konosuba : Sois Beni Monde Merveilleux !")
            .Should().Be("Konosuba");
        MediaIdentityKeys.SeriesShortName("DanMachi - La legende des Familias")
            .Should().Be("DanMachi");
        MediaIdentityKeys.SeriesShortName("Bref").Should().BeNull();
    }

    [Test]
    public void SeriesSubtitle_ShouldTakeTextAfterSeparator()
    {
        MediaIdentityKeys.SeriesSubtitle("Tsugai - Daemons of the Shadow Realm")
            .Should().Be("Daemons of the Shadow Realm");
        MediaIdentityKeys.SeriesSubtitle("CSI: Miami").Should().BeNull();
        MediaIdentityKeys.SeriesSubtitle("Bref").Should().BeNull();
    }

    [Test]
    public void SeriesTitlesOverlap_ShouldMatchEnglishSubtitle_WhenK7HasLocalPrefix()
    {
        MediaIdentityKeys.SeriesTitlesOverlap(
                "Daemons of the Shadow Realm",
                "Tsugai - Daemons of the Shadow Realm")
            .Should().BeTrue();
        MediaIdentityKeys.SeriesTitlesOverlap("DanMachi", "DanMachi: Sword Oratoria")
            .Should().BeFalse();
    }

    [Test]
    public void FindSeriesByShortNamePrefix_ShouldReturnBothKonosubaShows()
    {
        var series = new (string Title, string? Original)[]
        {
            ("Konosuba -God's blessing on this wonderful world!", null),
            ("KonoSuba - An Explosion on This Wonderful World!", null),
            ("One Piece", null)
        };

        var matches = MediaIdentityKeys.FindSeriesByShortNamePrefix(
            "Konosuba : Sois Beni Monde Merveilleux !",
            series,
            s => s.Title,
            s => s.Original);

        matches.Should().HaveCount(2);
    }

    [Test]
    public void DistinctiveLastToken_ShouldKeepMayfair()
    {
        MediaIdentityKeys.DistinctiveLastToken("Sorcieres de Mayfair").Should().Be("Mayfair");
    }

    [Test]
    public void AlbumTitlesOverlap_ShouldAllowAlbumPrefix()
    {
        MediaIdentityKeys.AlbumTitlesOverlap("Arcane", "Arcane: League of Legends: Season Two Original Soundtrack")
            .Should().BeTrue();
        MediaIdentityKeys.AlbumTitlesOverlap("Love", "Loveless").Should().BeFalse();
    }

    [Test]
    public void UnfoldCommaSortName_ShouldReversePersonSortName()
    {
        MediaIdentityKeys.UnfoldCommaSortName("Kondo, Koji").Should().Be("Koji Kondo");
        MediaIdentityKeys.UnfoldCommaSortName("Beatles, The").Should().Be("The Beatles");
    }

    [Test]
    public void UnfoldCommaSortName_ShouldLeaveAlbumSortTitlesAlone()
    {
        MediaIdentityKeys.UnfoldCommaSortName("Legend of Zelda, The: Ocarina of Time: Official Soundtrack")
            .Should().Be("Legend of Zelda, The: Ocarina of Time: Official Soundtrack");
    }

    [Test]
    public void PersonNamesMatch_ShouldAlignLatinNameAndMusicBrainzSortName()
    {
        MediaIdentityKeys.PersonNamesMatch("Koji Kondo", "Kondo, Koji").Should().BeTrue();
        MediaIdentityKeys.PersonNamesMatch("Koji Kondo", "近藤浩治").Should().BeFalse();
    }

    [Test]
    public void ResolveSeriesMatches_ShouldStayAmbiguous_WhenTwoYearSuffixedShowsShareTheBareTitle()
    {
        var series = new (string Title, string? Original)[]
        {
            ("Baki (2018)", null),
            ("Baki (2001)", null)
        };

        MediaIdentityKeys.ResolveSeriesMatches("Baki", series, s => s.Title, s => s.Original)
            .Should().BeEmpty();
    }

    [Test]
    public void TryParseSeasonEpisode_ShouldUseFirstEpisode_WhenFileIsCombined()
    {
        MediaIdentityKeys.TryParseSeasonEpisode(
            "The Office (US) - S07E025-E026 - Search Committee.mkv",
            out var season,
            out var episode).Should().BeTrue();

        season.Should().Be(7);
        episode.Should().Be(25);
    }

    [Test]
    public void TryParseSeasonEpisodeRange_ShouldReturnLastEpisode_WhenFileIsCombined()
    {
        MediaIdentityKeys.TryParseSeasonEpisodeRange(
            "The Office (US) - S07E025-E026 - Search Committee.mkv",
            out var season,
            out var first,
            out var last).Should().BeTrue();

        season.Should().Be(7);
        first.Should().Be(25);
        last.Should().Be(26);
    }

    [TestCase("Show.S01E01-S01E02.mkv", 1, 1, 2)]
    [TestCase("Show.S1E1-S1E2-S1E3-S1E4.mkv", 1, 1, 4)]
    [TestCase("Show.S1E01-E04.mkv", 1, 1, 4)]
    [TestCase("Show.1x01-1x03.mkv", 1, 1, 3)]
    public void TryParseSeasonEpisodeRange_ShouldAcceptRestatedAndMixedRanges(
        string text, int season, int first, int last)
    {
        MediaIdentityKeys.TryParseSeasonEpisodeRange(text, out var parsedSeason, out var parsedFirst, out var parsedLast)
            .Should().BeTrue();
        parsedSeason.Should().Be(season);
        parsedFirst.Should().Be(first);
        parsedLast.Should().Be(last);
    }

    [Test]
    public void TryParseSeasonEpisodeRange_ShouldKeepFirstEpisode_WhenSeasonChanges()
    {
        MediaIdentityKeys.TryParseSeasonEpisodeRange("Show.S01E01-S02E01.mkv", out var season, out var first, out var last)
            .Should().BeTrue();
        season.Should().Be(1);
        first.Should().Be(1);
        last.Should().Be(1);
    }
}
