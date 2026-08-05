using FluentAssertions;
using K7.Server.Application.Features.Medias.Services;
using NUnit.Framework;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class MusicArtistNameNormalizerTests
{
    [Test]
    public void FromId3v23SplitValues_ShouldKeepAcDcTogether_WhenTagLibSplitOnSlash()
    {
        var result = MusicArtistNameNormalizer.FromId3v23SplitValues(["AC", "DC"]);

        result.Should().Equal("AC/DC");
    }

    [Test]
    public void FromId3v23SplitValues_ShouldSplitSpacedSlash_WhenMultiArtist()
    {
        var result = MusicArtistNameNormalizer.FromId3v23SplitValues(["Alice ", " Bob"]);

        result.Should().Equal("Alice", "Bob");
    }

    [Test]
    public void FromId3v23SplitValues_ShouldSplitSemicolon_WhenMultiArtist()
    {
        var result = MusicArtistNameNormalizer.FromId3v23SplitValues(["Alice; Bob"]);

        result.Should().Equal("Alice", "Bob");
    }

    [Test]
    public void Split_ShouldKeepBareSlashNamesIntact()
    {
        MusicArtistNameNormalizer.Split("AC/DC").Should().Equal("AC/DC");
        MusicArtistNameNormalizer.Split("a/k/a").Should().Equal("a/k/a");
    }

    [Test]
    public void Split_ShouldSeparateOnSpacedSlashAndSemicolon()
    {
        MusicArtistNameNormalizer.Split("Alice / Bob").Should().Equal("Alice", "Bob");
        MusicArtistNameNormalizer.Split("Alice;Bob; Carol").Should().Equal("Alice", "Bob", "Carol");
    }

    [Test]
    public void FromId3v23SplitValues_ShouldReturnEmpty_WhenNullOrBlank()
    {
        MusicArtistNameNormalizer.FromId3v23SplitValues(null).Should().BeEmpty();
        MusicArtistNameNormalizer.FromId3v23SplitValues(["  ", ""]).Should().BeEmpty();
    }
}
