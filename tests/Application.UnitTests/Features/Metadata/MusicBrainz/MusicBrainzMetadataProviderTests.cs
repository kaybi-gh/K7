using K7.Server.Domain.Models;
using K7.Server.Infrastructure.MediaProcessing.MetadataProvider;

namespace K7.Server.Application.UnitTests.Features.Metadata.MusicBrainz;

[TestFixture]
public class MusicBrainzMetadataProviderTests
{
    [TestCase(null, null)]
    [TestCase("", null)]
    [TestCase("not-a-date", null)]
    public void ParseDate_ShouldReturnNull_WhenDateIsMissingOrInvalid(string? date, DateOnly? expected) =>
        MusicBrainzMetadataProvider.ParseDate(date).Should().Be(expected);

    [Test]
    public void ParseDate_ShouldParseYearOnly_WhenDateHasFourDigits() =>
        MusicBrainzMetadataProvider.ParseDate("2001").Should().Be(new DateOnly(2001, 1, 1));

    [Test]
    public void ParseDate_ShouldParseYearAndMonth_WhenDateHasSevenCharacters() =>
        MusicBrainzMetadataProvider.ParseDate("2001-03").Should().Be(new DateOnly(2001, 3, 1));

    [Test]
    public void ParseDate_ShouldParseFullDate_WhenDateIsComplete() =>
        MusicBrainzMetadataProvider.ParseDate("2001-03-12").Should().Be(new DateOnly(2001, 3, 12));

    [Test]
    public void ExtractQid_ShouldReturnQid_WhenLastSegmentStartsWithQ() =>
        MusicBrainzMetadataProvider.ExtractQid("https://www.wikidata.org/wiki/Q123").Should().Be("Q123");

    [Test]
    public void ExtractQid_ShouldReturnNull_WhenLastSegmentDoesNotStartWithQ() =>
        MusicBrainzMetadataProvider.ExtractQid("https://www.wikidata.org/wiki/NotAQid").Should().BeNull();

    [Test]
    public void ExtractSpotifyId_ShouldReturnLastSegment_WhenUrlIsValid() =>
        MusicBrainzMetadataProvider.ExtractSpotifyId("https://open.spotify.com/artist/4Z8W4fKeB5YxbusRsdQVPb")
            .Should().Be("4Z8W4fKeB5YxbusRsdQVPb");

    [Test]
    public void ExtractSpotifyId_ShouldReturnNull_WhenUrlHasSingleSegment() =>
        MusicBrainzMetadataProvider.ExtractSpotifyId("https://open.spotify.com/artist").Should().BeNull();

    [Test]
    public void ExtractImdbId_ShouldReturnNameId_WhenUrlIsValid() =>
        MusicBrainzMetadataProvider.ExtractImdbId("https://www.imdb.com/name/nm0000093/").Should().Be("nm0000093");

    [Test]
    public void BuildSearchQuery_ShouldCombineAlbumAndArtist_WithoutHardDateFilter()
    {
        var identification = new MediaIdentification("fallback title")
        {
            AlbumName = "Discovery",
            ArtistName = "Daft Punk",
            ReleaseYear = new DateOnly(2001, 3, 12)
        };

        var query = MusicBrainzMetadataProvider.BuildSearchQuery(identification);

        query.Should().Be("release:\"Discovery\" AND artist:\"Daft Punk\"");
    }

    [Test]
    public void BuildSearchQuery_ShouldPreferArtistMbid_WhenPresent()
    {
        var identification = new MediaIdentification("fallback")
        {
            AlbumName = "No Strings Attached",
            ArtistName = "*NSYNC",
            MusicBrainzAlbumArtistId = "603ba565-3967-4be1-931e-9cb945394e86"
        };

        var query = MusicBrainzMetadataProvider.BuildSearchQuery(identification);

        query.Should().Be("release:\"No Strings Attached\" AND arid:603ba565-3967-4be1-931e-9cb945394e86");
    }

    [Test]
    public void BuildSearchQuery_ShouldEscapeLuceneSpecialCharacters_InQuotedFields()
    {
        var identification = new MediaIdentification("fallback")
        {
            AlbumName = "No Strings Attached",
            ArtistName = "*NSYNC"
        };

        var query = MusicBrainzMetadataProvider.BuildSearchQuery(identification);

        query.Should().Be("release:\"No Strings Attached\" AND artist:\"\\*NSYNC\"");
    }

    [Test]
    public void EscapeLucene_ShouldEscapeParenthesesAndAsterisk()
    {
        MusicBrainzMetadataProvider.EscapeLucene("*NSYNC").Should().Be("\\*NSYNC");
        MusicBrainzMetadataProvider.EscapeLucene("Song (Remix)").Should().Be("Song \\(Remix\\)");
    }

    [Test]
    public void NormalizeArtistSearchName_ShouldStripLeadingPunctuation()
    {
        MusicBrainzMetadataProvider.NormalizeArtistSearchName("*NSYNC").Should().Be("NSYNC");
        MusicBrainzMetadataProvider.NormalizeArtistSearchName("'N Sync").Should().Be("N Sync");
    }

    [Test]
    public void BuildSearchQuery_ShouldFallBackToTitle_WhenAlbumNameIsMissing()
    {
        var identification = new MediaIdentification("Homework");

        var query = MusicBrainzMetadataProvider.BuildSearchQuery(identification);

        query.Should().Be("release:\"Homework\"");
    }

    [Test]
    public void FindPreferredYearIndex_ShouldReturnMatchingIndex_WhenYearHintMatches()
    {
        var index = MusicBrainzMetadataProvider.FindPreferredYearIndex(
            ["1998-01-01", "2000-03-21", "2001"],
            preferredYear: 2000);

        index.Should().Be(1);
    }

    [Test]
    public void FindPreferredYearIndex_ShouldReturnNull_WhenYearHintMissingOrUnmatched()
    {
        MusicBrainzMetadataProvider.FindPreferredYearIndex(["2000"], preferredYear: null)
            .Should().BeNull();
        MusicBrainzMetadataProvider.FindPreferredYearIndex(["2000"], preferredYear: 1999)
            .Should().BeNull();
        MusicBrainzMetadataProvider.FindPreferredYearIndex([], preferredYear: 2000)
            .Should().BeNull();
    }
}
