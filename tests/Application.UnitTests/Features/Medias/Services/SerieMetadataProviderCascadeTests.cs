using K7.Server.Application.Common;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;

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
    public void ResolveSearchProviders_ShouldSearchBothWithoutBias_WhenPrimaryIsAuto()
    {
        SerieMetadataProviderCascade.ResolveSearchProviders("auto")
            .Should().Equal(MetadataProviderNames.Tmdb, MetadataProviderNames.Tvdb);
        SerieMetadataProviderCascade.IsAuto("auto").Should().BeTrue();
    }

    [Test]
    public void ResolveEnrichmentProvider_ShouldReturnAlternate()
    {
        SerieMetadataProviderCascade.ResolveEnrichmentProvider("tvdb").Should().Be(MetadataProviderNames.Tmdb);
        SerieMetadataProviderCascade.ResolveEnrichmentProvider("tmdb").Should().Be(MetadataProviderNames.Tvdb);
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

    [Test]
    public void ResolveKeyedProviderName_ShouldKeepConcreteProvider()
    {
        SerieMetadataProviderCascade.ResolveKeyedProviderName("tvdb").Should().Be(MetadataProviderNames.Tvdb);
        SerieMetadataProviderCascade.ResolveKeyedProviderName("tmdb").Should().Be(MetadataProviderNames.Tmdb);
        SerieMetadataProviderCascade.ResolveKeyedProviderName("federation").Should().Be("federation");
        SerieMetadataProviderCascade.ResolveKeyedProviderName("imdb").Should().Be(MetadataProviderNames.Tmdb);
    }

    [Test]
    public void ResolveKeyedProviderName_ShouldUseMatchingExternalId_WhenRequestedIsAuto()
    {
        var ids = new[]
        {
            new ExternalId { ProviderName = MetadataProviderNames.Tvdb, Value = "123" },
            new ExternalId { ProviderName = MetadataProviderNames.Tmdb, Value = "456" }
        };

        SerieMetadataProviderCascade.ResolveKeyedProviderName(
                MetadataProviderNames.Auto,
                numberingProviderName: MetadataProviderNames.Tmdb,
                externalIds: ids,
                requestedExternalId: "123")
            .Should().Be(MetadataProviderNames.Tvdb);
    }

    [Test]
    public void ResolveKeyedProviderName_ShouldUseNumberingProvider_WhenAutoHasNoMatchingId()
    {
        SerieMetadataProviderCascade.ResolveKeyedProviderName(
                MetadataProviderNames.Auto,
                numberingProviderName: MetadataProviderNames.Tvdb)
            .Should().Be(MetadataProviderNames.Tvdb);
    }

    [Test]
    public void ResolveKeyedProviderName_ShouldDefaultToTmdb_WhenAutoHasNoHint()
    {
        SerieMetadataProviderCascade.ResolveKeyedProviderName(MetadataProviderNames.Auto)
            .Should().Be(MetadataProviderNames.Tmdb);
    }
}
