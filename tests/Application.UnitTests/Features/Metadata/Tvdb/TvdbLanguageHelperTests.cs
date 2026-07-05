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
}
