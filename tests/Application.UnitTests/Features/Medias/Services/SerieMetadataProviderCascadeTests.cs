using K7.Server.Application.Common;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class SerieMetadataProviderCascadeTests
{
    [Test]
    public void ResolveSearchProviders_ShouldPreferTvdbThenTmdb_WhenPrimaryIsTvdb()
    {
        SerieMetadataProviderCascade.ResolveSearchProviders("tvdb")
            .Should().Equal(MetadataProviderNames.Tvdb, MetadataProviderNames.Tmdb);
    }

    [Test]
    public void ResolveSearchProviders_ShouldPreferTmdbThenTvdb_WhenPrimaryIsTmdb()
    {
        SerieMetadataProviderCascade.ResolveSearchProviders("tmdb")
            .Should().Equal(MetadataProviderNames.Tmdb, MetadataProviderNames.Tvdb);
    }

    [Test]
    public void ResolveSearchProviders_ShouldMapImdbPrimaryToTmdbCascade()
    {
        SerieMetadataProviderCascade.ResolveSearchProviders("imdb")
            .Should().Equal(MetadataProviderNames.Tmdb, MetadataProviderNames.Tvdb);
    }

    [Test]
    public void ResolveSearchProviders_ShouldNotCascadeFederation()
    {
        SerieMetadataProviderCascade.ResolveSearchProviders("federation")
            .Should().Equal("federation");
    }

    [Test]
    public void IsCascadeProvider_ShouldAcceptFallbackProvider()
    {
        SerieMetadataProviderCascade.IsCascadeProvider("tmdb", "tvdb").Should().BeTrue();
        SerieMetadataProviderCascade.IsCascadeProvider("tvdb", "tmdb").Should().BeTrue();
        SerieMetadataProviderCascade.IsCascadeProvider("musicbrainz", "tvdb").Should().BeFalse();
    }
}
