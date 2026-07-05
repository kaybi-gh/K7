using K7.Server.Infrastructure.MediaProcessing.MetadataProvider.Tvdb;

namespace K7.Server.Application.UnitTests.Features.Metadata.Tvdb;

[TestFixture]
public class TvdbTranslationResolverTests
{
    [Test]
    public void BuildLanguagePriority_ShouldPreferRequestedThenFallbackThenOriginal()
    {
        var languages = TvdbTranslationResolver.BuildLanguagePriority("en", "fr", "jpn");

        languages.Should().Equal("eng", "fra", "jpn");
    }

    [Test]
    public void BuildLanguagePriority_ShouldDeduplicateLanguages()
    {
        var languages = TvdbTranslationResolver.BuildLanguagePriority("en", "en", "eng");

        languages.Should().Equal("eng");
    }
}
