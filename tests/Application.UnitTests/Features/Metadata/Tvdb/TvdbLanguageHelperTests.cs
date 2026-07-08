using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

namespace K7.Server.Application.UnitTests.Features.Metadata.Tvdb;

[TestFixture]
public class TvdbLanguageHelperTests
{
    [TestCase("en", "eng")]
    [TestCase("fr", "fra")]
    [TestCase("FR-fr", "fra")]
    [TestCase("eng", "eng")]
    public void ToTvdbLanguage_ShouldMapIso6391(string input, string expected) =>
        TvdbLanguageHelper.ToTvdbLanguage(input).Should().Be(expected);

    [Test]
    public void ToTvdbLanguage_ShouldDefaultToEnglish_WhenUnknown() =>
        TvdbLanguageHelper.ToTvdbLanguage("xx").Should().Be("eng");

    [TestCase("eng", "en")]
    [TestCase("fra", "fr")]
    [TestCase("jpn", "ja")]
    [TestCase("en", "en")]
    [TestCase("EN", "en")]
    public void ToIso6391_ShouldMapTvdbToIso6391(string input, string expected) =>
        TvdbLanguageHelper.ToIso6391(input).Should().Be(expected);

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void ToIso6391_ShouldReturnNull_WhenEmpty(string? input) =>
        TvdbLanguageHelper.ToIso6391(input).Should().BeNull();

    [Test]
    public void ToIso6391_ShouldReturnNull_WhenUnknownTvdbCode() =>
        TvdbLanguageHelper.ToIso6391("xyz").Should().BeNull();
}
