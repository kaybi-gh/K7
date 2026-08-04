using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class MetadataSearchLanguageHelperTests
{
    [Test]
    public void ResolveSearchLanguages_ShouldReturnPrimaryThenFallback()
    {
        var languages = MetadataSearchLanguageHelper.ResolveSearchLanguages("fr-FR", "en-US");

        languages.Should().Equal("fr-FR", "en-US");
    }

    [Test]
    public void ResolveSearchLanguages_ShouldDedupSameLanguageFamily()
    {
        var languages = MetadataSearchLanguageHelper.ResolveSearchLanguages("fr", "fr-FR");

        languages.Should().Equal("fr");
    }

    [Test]
    public void ResolveSearchLanguages_ShouldReturnEmpty_WhenNeitherSet()
    {
        MetadataSearchLanguageHelper.ResolveSearchLanguages(null, "  ")
            .Should().BeEmpty();
    }

    [Test]
    public void ResolveSearchLanguages_ShouldKeepSingleLanguage_WhenFallbackMissing()
    {
        MetadataSearchLanguageHelper.ResolveSearchLanguages("en", null)
            .Should().Equal("en");
    }
}
