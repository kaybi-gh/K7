using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

namespace K7.Server.Application.UnitTests.Features.Metadata.Tvdb;

[TestFixture]
public class TvdbTranslationResolverTests
{
    [Test]
    public void BuildLanguagePriority_ShouldPreferRequestedThenFallback()
    {
        var languages = TvdbTranslationResolver.BuildLanguagePriority("en", "fr");

        languages.Should().Equal("eng", "fra");
    }

    [Test]
    public void BuildLanguagePriority_ShouldDeduplicateLanguages()
    {
        var languages = TvdbTranslationResolver.BuildLanguagePriority("en", "en", "eng");

        languages.Should().Equal("eng");
    }

    [Test]
    public void BuildLanguagePriority_ShouldIgnoreOriginalLanguage()
    {
        var languages = TvdbTranslationResolver.BuildLanguagePriority("fr", "en", "jpn");

        languages.Should().Equal("fra", "eng");
    }

    [Test]
    public void PickTranslatedText_ShouldSkipJapaneseFrenchRecord_AndUseEnglish()
    {
        var (title, overview) = TvdbTranslationResolver.PickTranslatedText(
            "ブリーチ",
            "日本語のあらすじ",
            "fr",
            [
                ("ブリーチ", "Un synopsis francais"),
                ("Bleach", "A substitute shinigami")
            ]);

        title.Should().Be("Bleach");
        overview.Should().Be("Un synopsis francais");
    }

    [Test]
    public void PickTranslatedText_ShouldKeepLatinBaseTitle_WhenTranslationsMissing()
    {
        var (title, overview) = TvdbTranslationResolver.PickTranslatedText(
            "Bleach",
            null,
            "fr",
            []);

        title.Should().Be("Bleach");
        overview.Should().BeNull();
    }
}
