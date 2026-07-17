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
    public void BuildSearchQuery_ShouldCombineAlbumArtistAndYear_WhenAllPresent()
    {
        var identification = new MediaIdentification("fallback title")
        {
            AlbumName = "Discovery",
            ArtistName = "Daft Punk",
            ReleaseYear = new DateOnly(2001, 3, 12)
        };

        var query = MusicBrainzMetadataProvider.BuildSearchQuery(identification);

        query.Should().Be("release:\"Discovery\" AND artist:\"Daft Punk\" AND date:2001");
    }

    [Test]
    public void BuildSearchQuery_ShouldFallBackToTitle_WhenAlbumNameIsMissing()
    {
        var identification = new MediaIdentification("Homework");

        var query = MusicBrainzMetadataProvider.BuildSearchQuery(identification);

        query.Should().Be("release:\"Homework\"");
    }
}
