using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Shared.Dtos.Entities.Metadatas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class SerieMetadataIdentityServiceTests
{
    [Test]
    public async Task ResolveAsync_ShouldPreferMatchingYear_WhenHomonymsAcrossProviders()
    {
        var (tmdb, tmdbSearch) = CreateSearchableSerieProvider(
            "tmdb",
            [
                new MetadataSearchResult
                {
                    Provider = "tmdb",
                    ExternalId = "tmdb-2023",
                    Title = "The Buccaneers",
                    Year = 2023,
                    Popularity = 40
                }
            ],
            fetchIds: id =>
            [
                new ExternalId { ProviderName = "tmdb", Value = id },
                new ExternalId { ProviderName = "tvdb", Value = "tvdb-2023" }
            ],
            episodeKeys: [(1, 1)]);

        var (tvdb, tvdbSearch) = CreateSearchableSerieProvider(
            "tvdb",
            [
                new MetadataSearchResult
                {
                    Provider = "tvdb",
                    ExternalId = "tvdb-1956",
                    Title = "The Buccaneers",
                    Year = 1956,
                    Popularity = 2
                }
            ],
            fetchIds: id =>
            [
                new ExternalId { ProviderName = "tvdb", Value = id }
            ],
            episodeKeys: [(1, 1)]);

        var sp = BuildProvider(tmdb, tvdb);
        var sut = new SerieMetadataIdentityService(
            [tmdbSearch, tvdbSearch],
            sp,
            Substitute.For<ILogger<SerieMetadataIdentityService>>());

        var match = await sut.ResolveAsync(
            new MediaIdentification("The Buccaneers")
            {
                SeriesTitle = "The Buccaneers",
                ReleaseYear = new DateOnly(2023, 1, 1)
            },
            libraryProviderName: "auto",
            fileIdentifications:
            [
                new MediaIdentification("ep") { SeasonNumber = 1, EpisodeNumber = 1 }
            ],
            language: "en",
            fallbackLanguage: null);

        match.Should().NotBeNull();
        match!.NumberingExternalId.Should().BeOneOf("tmdb-2023", "tvdb-2023");
        match.ExternalIds.Should().Contain(e => e.ExternalId == "tmdb-2023");
        await sp.DisposeAsync();
    }

    [Test]
    public async Task ResolveAsync_ShouldPickTmdbCanon_WhenAutoHitRateFavorsStandardSeasons()
    {
        var (tmdb, tmdbSearch) = CreateSearchableSerieProvider(
            "tmdb",
            [
                new MetadataSearchResult
                {
                    Provider = "tmdb",
                    ExternalId = "tmdb-taratata",
                    Title = "Taratata",
                    Year = 1993,
                    Popularity = 10
                }
            ],
            fetchIds: _ =>
            [
                new ExternalId { ProviderName = "tmdb", Value = "tmdb-taratata" },
                new ExternalId { ProviderName = "tvdb", Value = "tvdb-taratata" }
            ],
            episodeKeys: [(1, 1), (1, 2), (1, 3)]);

        var (tvdb, tvdbSearch) = CreateSearchableSerieProvider(
            "tvdb",
            [
                new MetadataSearchResult
                {
                    Provider = "tvdb",
                    ExternalId = "tvdb-taratata",
                    Title = "Taratata",
                    Year = 1993,
                    Popularity = 12
                }
            ],
            fetchIds: _ =>
            [
                new ExternalId { ProviderName = "tvdb", Value = "tvdb-taratata" },
                new ExternalId { ProviderName = "tmdb", Value = "tmdb-taratata" }
            ],
            episodeKeys: [(1993, 1), (1994, 1)]);

        var sp = BuildProvider(tmdb, tvdb);
        var sut = new SerieMetadataIdentityService(
            [tmdbSearch, tvdbSearch],
            sp,
            Substitute.For<ILogger<SerieMetadataIdentityService>>());

        var files = new[]
        {
            new MediaIdentification("ep1") { SeasonNumber = 1, EpisodeNumber = 1 },
            new MediaIdentification("ep2") { SeasonNumber = 1, EpisodeNumber = 2 },
            new MediaIdentification("ep3") { SeasonNumber = 1, EpisodeNumber = 3 }
        };

        var match = await sut.ResolveAsync(
            new MediaIdentification("Taratata")
            {
                SeriesTitle = "Taratata",
                ReleaseYear = new DateOnly(1993, 1, 1)
            },
            libraryProviderName: "auto",
            fileIdentifications: files,
            language: "en",
            fallbackLanguage: null);

        match.Should().NotBeNull();
        match!.NumberingProviderName.Should().Be("tmdb");
        match.NumberingExternalId.Should().Be("tmdb-taratata");
        match.ExternalIds.Should().Contain(e => e.ProviderName == "tvdb" && e.ExternalId == "tvdb-taratata");
        await sp.DisposeAsync();
    }

    [Test]
    public async Task ResolveAsync_ShouldShortCircuitOnPathId_WhenPresent()
    {
        var tmdb = Substitute.For<ISerieMetadataProvider>();
        tmdb.ProviderName.Returns("tmdb");
        tmdb.FetchSerieMetadataAsync("213338", Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(new ExternalSerieMetadata
            {
                Title = "The Buccaneers",
                ExternalIds =
                [
                    new ExternalId { ProviderName = "tmdb", Value = "213338" },
                    new ExternalId { ProviderName = "tvdb", Value = "999" }
                ]
            });
        tmdb.ListEpisodeKeysAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<(int, int)> { (1, 1) });

        var tvdb = Substitute.For<ISerieMetadataProvider>();
        tvdb.ProviderName.Returns("tvdb");
        tvdb.ListEpisodeKeysAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<(int, int)> { (1, 1) });

        var sp = BuildProvider(tmdb, tvdb);
        var sut = new SerieMetadataIdentityService(
            Enumerable.Empty<ISearchableMetadataProvider>(),
            sp,
            Substitute.For<ILogger<SerieMetadataIdentityService>>());

        var match = await sut.ResolveAsync(
            new MediaIdentification("The Buccaneers")
            {
                SeriesTitle = "The Buccaneers",
                ProviderName = "tmdb",
                ProviderExternalId = "213338",
                SeasonNumber = 1,
                EpisodeNumber = 1
            },
            libraryProviderName: "auto",
            fileIdentifications:
            [
                new MediaIdentification("ep") { SeasonNumber = 1, EpisodeNumber = 1 }
            ],
            language: "en",
            fallbackLanguage: null);

        match.Should().NotBeNull();
        match!.NumberingExternalId.Should().BeOneOf("213338", "999");
        match.ExternalIds.Should().Contain(e => e.ExternalId == "213338");
        await tmdb.DidNotReceive().SearchSerieAsync(
            Arg.Any<MediaIdentification>(),
            Arg.Any<string?>(),
            Arg.Any<string?>(),
            Arg.Any<CancellationToken>());
        await sp.DisposeAsync();
    }

    [Test]
    public async Task ResolveAsync_ShouldKeepForcedTvdbCanon_WhenLibraryForcesTvdbDespiteTmdbHitRate()
    {
        var (tmdb, tmdbSearch) = CreateSearchableSerieProvider(
            "tmdb",
            [
                new MetadataSearchResult
                {
                    Provider = "tmdb",
                    ExternalId = "tmdb-1",
                    Title = "Show",
                    Year = 2000,
                    Popularity = 50
                }
            ],
            fetchIds: _ =>
            [
                new ExternalId { ProviderName = "tmdb", Value = "tmdb-1" },
                new ExternalId { ProviderName = "tvdb", Value = "tvdb-1" }
            ],
            episodeKeys: [(1, 1)]);

        var (tvdb, tvdbSearch) = CreateSearchableSerieProvider(
            "tvdb",
            [
                new MetadataSearchResult
                {
                    Provider = "tvdb",
                    ExternalId = "tvdb-1",
                    Title = "Show",
                    Year = 2000,
                    Popularity = 10
                }
            ],
            fetchIds: _ =>
            [
                new ExternalId { ProviderName = "tvdb", Value = "tvdb-1" },
                new ExternalId { ProviderName = "tmdb", Value = "tmdb-1" }
            ],
            episodeKeys: []);

        var sp = BuildProvider(tmdb, tvdb);
        var sut = new SerieMetadataIdentityService(
            [tmdbSearch, tvdbSearch],
            sp,
            Substitute.For<ILogger<SerieMetadataIdentityService>>());

        var match = await sut.ResolveAsync(
            new MediaIdentification("Show")
            {
                SeriesTitle = "Show",
                ReleaseYear = new DateOnly(2000, 1, 1)
            },
            libraryProviderName: "tvdb",
            fileIdentifications:
            [
                new MediaIdentification("ep") { SeasonNumber = 1, EpisodeNumber = 1 }
            ],
            language: "en",
            fallbackLanguage: null);

        match.Should().NotBeNull();
        match!.NumberingProviderName.Should().Be("tvdb");
        match.NumberingExternalId.Should().Be("tvdb-1");
        await sp.DisposeAsync();
    }

    private static ServiceProvider BuildProvider(ISerieMetadataProvider tmdb, ISerieMetadataProvider tvdb)
    {
        var services = new ServiceCollection();
        services.AddKeyedSingleton("tmdb", tmdb);
        services.AddKeyedSingleton("tvdb", tvdb);
        return services.BuildServiceProvider();
    }

    private static (ISerieMetadataProvider Serie, ISearchableMetadataProvider Search) CreateSearchableSerieProvider(
        string providerName,
        IReadOnlyList<MetadataSearchResult> searchResults,
        Func<string, IList<ExternalId>> fetchIds,
        IReadOnlyList<(int Season, int Episode)> episodeKeys)
    {
        var serie = Substitute.For<ISerieMetadataProvider, ISearchableMetadataProvider>();
        serie.ProviderName.Returns(providerName);
        ((ISearchableMetadataProvider)serie).ProviderName.Returns(providerName);

        ((ISearchableMetadataProvider)serie).SearchMetadataAsync(
                Arg.Any<string>(),
                Arg.Any<int?>(),
                Arg.Any<string?>(),
                Arg.Any<MediaType?>(),
                Arg.Any<string>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>())
            .Returns(searchResults);

        serie.FetchSerieMetadataAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<string?>())
            .Returns(callInfo => new ExternalSerieMetadata
            {
                Title = searchResults.FirstOrDefault()?.Title ?? providerName,
                ExternalIds = fetchIds(callInfo.ArgAt<string>(0))
            });

        serie.ListEpisodeKeysAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(episodeKeys.ToHashSet());

        return (serie, (ISearchableMetadataProvider)serie);
    }
}
