using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.BulkCreateMedias;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Requests;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class BulkCreateMediasCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private BulkCreateMediasCommandHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();

        _sender = Substitute.For<ISender>();
        _handler = new BulkCreateMediasCommandHandler(_context, _sender, Array.Empty<IMetadataProviderInfo>(), new MediaIdentityLookupService(_context));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReuseExistingMedia_WhenExternalIdMatches()
    {
        var existingId = Guid.NewGuid();
        var movie = new Movie { Id = existingId, Title = "Existing" };
        movie.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "42" });
        _context.Medias.Add(movie);
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "k1",
                    MediaType = "movie",
                    Title = "Different Title",
                    ExternalIds = new Dictionary<string, string> { ["tmdb"] = "42" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(existingId);
        response.Results[0].WasCreated.Should().BeFalse();
        (await _context.Medias.OfType<Movie>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldOmitUnmatched_WhenCreateMissingFalse()
    {
        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "missing",
                    MediaType = "movie",
                    Title = "Nowhere"
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().BeEmpty();
        (await _context.Medias.CountAsync()).Should().Be(0);
    }

    [Test]
    public async Task Handle_ShouldCreateMovieMusicAndEpisode()
    {
        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "m1",
                    MediaType = "movie",
                    Title = "Inception",
                    Year = 2010,
                    ExternalIds = new Dictionary<string, string> { ["tmdb"] = "1" }
                },
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "t1",
                    MediaType = "music",
                    Title = "Song",
                    ArtistName = "Artist",
                    AlbumName = "Album"
                },
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "e1",
                    MediaType = "episode",
                    Title = "Pilot",
                    SeriesTitle = "Show",
                    SeasonNumber = 1,
                    EpisodeNumber = 1
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().HaveCount(3);
        response.Results.Should().OnlyContain(r => r.WasCreated);
        (await _context.Medias.OfType<Movie>().CountAsync()).Should().Be(1);
        (await _context.Medias.OfType<MusicTrack>().CountAsync()).Should().Be(1);
        (await _context.Medias.OfType<MusicAlbum>().CountAsync()).Should().Be(1);
        (await _context.Medias.OfType<MusicArtist>().CountAsync()).Should().Be(1);
        (await _context.Medias.OfType<SerieEpisode>().CountAsync()).Should().Be(1);
        (await _context.Medias.OfType<Serie>().CountAsync()).Should().Be(1);
        (await _context.Medias.OfType<SerieSeason>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldDedupIntraBatchByExternalId()
    {
        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "a",
                    MediaType = "movie",
                    Title = "Film",
                    ExternalIds = new Dictionary<string, string> { ["tmdb"] = "99" }
                },
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "b",
                    MediaType = "movie",
                    Title = "Film Alt",
                    ExternalIds = new Dictionary<string, string> { ["tmdb"] = "99" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().HaveCount(2);
        response.Results[0].MediaId.Should().Be(response.Results[1].MediaId);
        response.Results.Should().OnlyContain(r => r.WasCreated);
        (await _context.Medias.OfType<Movie>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldMatchMovieByTitleYear_WhenIndexedFileExists()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Movie);
        var movieId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = movieId,
            Title = "Match Me",
            ReleaseDate = new DateOnly(2015, 1, 1)
        };
        movie.IndexedFiles.Add(CreateIndexedFile(libraryId, movieId));
        _context.Medias.Add(movie);
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "hit",
                    MediaType = "movie",
                    Title = "Match Me",
                    Year = 2015
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(movieId);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchMusicByIsrc_WhenIndexedFileExists()
    {
        var trackId = SeedPlayableTrack("Cerf-volant", "Bruno Coulais", "Les Choristes");
        var track = await _context.Medias.OfType<MusicTrack>().SingleAsync(t => t.Id == trackId);
        track.ExternalIds.Add(new ExternalId { ProviderName = "isrc", Value = "FR26S2004011" });
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "spotify-1",
                    MediaType = "music",
                    Title = "Cerf volant",
                    ArtistName = "Bruno Coulais",
                    ExternalIds = new Dictionary<string, string>
                    {
                        ["spotify"] = "2aJhlFj1DOv6yDVfAWlWDc",
                        ["isrc"] = "FR26S2004011"
                    }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(trackId);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchMusicByTitle_WhenApostropheAndOriginalVersionDiffer()
    {
        var trackId = SeedPlayableTrack("Ain\u2019t No Rest for the Wicked", "Cage the Elephant", "Cage the Elephant");

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "s1",
                    MediaType = "music",
                    Title = "Ain't No Rest for the Wicked",
                    ArtistName = "Cage The Elephant",
                    ExternalIds = new Dictionary<string, string> { ["spotify"] = "4B3qEctMpLkb7eewdrQ6lU" }
                },
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "s2",
                    MediaType = "music",
                    Title = "Ain't No Rest For The Wicked - Original Version",
                    ArtistName = "Cage The Elephant",
                    ExternalIds = new Dictionary<string, string> { ["spotify"] = "5c5a2Ptu8eyIpljhQHjIqk" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().HaveCount(2);
        response.Results.Should().OnlyContain(r => r.MediaId == trackId && !r.WasCreated);
    }

    [Test]
    public async Task Handle_ShouldMatchMusicByTitle_WhenHyphenAndSpaceDiffer()
    {
        var trackId = SeedPlayableTrack("Cerf-volant", "Bruno Coulais", "Les Choristes");

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "cerf",
                    MediaType = "music",
                    Title = "Cerf volant",
                    ArtistName = "Bruno Coulais",
                    ExternalIds = new Dictionary<string, string> { ["spotify"] = "2aJhlFj1DOv6yDVfAWlWDc" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(trackId);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldNotMatchMusicCover_WhenArtistDiffers()
    {
        SeedPlayableTrack("Ain\u2019t No Rest for the Wicked", "Cage the Elephant", "Cage the Elephant");

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "cover",
                    MediaType = "music",
                    Title = "Ain't No Rest for the Wicked",
                    ArtistName = "Scott Bradlee's Postmodern Jukebox",
                    ExternalIds = new Dictionary<string, string> { ["spotify"] = "5337BdskAAG3MYToonX7hq" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldGroupMusicOntoExisting_WhenSiblingMatchesByIsrc()
    {
        var trackId = SeedPlayableTrack("Library recording", "Unknown Artist", "Unknown Album");
        var track = await _context.Medias.OfType<MusicTrack>().SingleAsync(t => t.Id == trackId);
        track.ExternalIds.Add(new ExternalId { ProviderName = "isrc", Value = "USAT29900865" });
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "popular",
                    MediaType = "music",
                    Title = "(Sittin' On) The Dock of the Bay",
                    ArtistName = "Otis Redding",
                    Popularity = 82,
                    ExternalIds = new Dictionary<string, string>
                    {
                        ["spotify"] = "3zBhihYUHBmGd2bcQIobrF",
                        ["isrc"] = "USAT21403443"
                    }
                },
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "k7-isrc",
                    MediaType = "music",
                    Title = "(Sittin' On) The Dock of the Bay",
                    ArtistName = "Otis Redding",
                    Popularity = 40,
                    ExternalIds = new Dictionary<string, string>
                    {
                        ["spotify"] = "1KbwA1skWj7LCn3B86VL8t",
                        ["isrc"] = "USAT29900865"
                    }
                },
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "other",
                    MediaType = "music",
                    Title = "(Sittin' On) The Dock of the Bay - Remastered",
                    ArtistName = "Otis Redding",
                    Popularity = 55,
                    ExternalIds = new Dictionary<string, string>
                    {
                        ["spotify"] = "4KqlR4kPbqZ0z0yPHWdYIc",
                        ["isrc"] = "USAT29900865"
                    }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().HaveCount(3);
        response.Results.Should().OnlyContain(r => r.MediaId == trackId && !r.WasCreated);
        (await _context.Medias.OfType<MusicTrack>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldKeepPopularIsrcOnly_WhenCreatingGroupedMusic()
    {
        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "low",
                    MediaType = "music",
                    Title = "(Sittin' On) The Dock of the Bay",
                    ArtistName = "Otis Redding",
                    AlbumName = "The Dock of the Bay",
                    Popularity = 40,
                    ExternalIds = new Dictionary<string, string>
                    {
                        ["spotify"] = "low-spotify",
                        ["isrc"] = "USAT21403443"
                    }
                },
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "high",
                    MediaType = "music",
                    Title = "(Sittin' On) The Dock of the Bay",
                    ArtistName = "Otis Redding",
                    AlbumName = "The Dock of the Bay",
                    Popularity = 82,
                    ExternalIds = new Dictionary<string, string>
                    {
                        ["spotify"] = "high-spotify",
                        ["isrc"] = "USAT29900865"
                    },
                    AdditionalSpotifyIds = ["extra-spotify"]
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().HaveCount(2);
        response.Results.Select(r => r.MediaId).Distinct().Should().ContainSingle();
        (await _context.Medias.OfType<MusicTrack>().CountAsync()).Should().Be(1);

        var created = await _context.Medias.OfType<MusicTrack>().Include(t => t.ExternalIds).SingleAsync();
        created.ExternalIds.Should().ContainSingle(e => e.ProviderName == "isrc");
        created.ExternalIds.Should().Contain(e => e.ProviderName == "isrc" && e.Value == "USAT29900865");
        created.ExternalIds.Should().NotContain(e => e.Value == "USAT21403443");
        created.ExternalIds.Count(e => e.ProviderName == "spotify").Should().Be(3);
    }

    [Test]
    public async Task Handle_ShouldMatchMusicByTitle_WhenExistingTrackIsVirtual()
    {
        var trackId = SeedVirtualTrack("Ain't No Rest for the Wicked", "Cage the Elephant", "Cage the Elephant");

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "s1",
                    MediaType = "music",
                    Title = "Ain't No Rest For The Wicked - Original Version",
                    ArtistName = "Cage The Elephant",
                    ExternalIds = new Dictionary<string, string> { ["spotify"] = "5c5a2Ptu8eyIpljhQHjIqk" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(trackId);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldDedupIntraBatchMusic_WhenSpotifyIdsDifferButTitleMatches()
    {
        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "a",
                    MediaType = "music",
                    Title = "Ain't No Rest for the Wicked",
                    ArtistName = "Cage The Elephant",
                    AlbumName = "Cage the Elephant",
                    ExternalIds = new Dictionary<string, string> { ["spotify"] = "4B3qEctMpLkb7eewdrQ6lU" }
                },
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "b",
                    MediaType = "music",
                    Title = "Ain't No Rest For The Wicked - Original Version",
                    ArtistName = "Cage The Elephant",
                    AlbumName = "Cage the Elephant",
                    ExternalIds = new Dictionary<string, string> { ["spotify"] = "5c5a2Ptu8eyIpljhQHjIqk" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().HaveCount(2);
        response.Results[0].MediaId.Should().Be(response.Results[1].MediaId);
        (await _context.Medias.OfType<MusicTrack>().CountAsync()).Should().Be(1);
        var created = await _context.Medias.OfType<MusicTrack>().Include(t => t.ExternalIds).SingleAsync();
        created.ExternalIds.Should().HaveCount(2);
    }

    [Test]
    public async Task Handle_ShouldMatchEpisodeBySeasonNumber_WhenSeriesHasCountrySuffixAndTitleDiffers()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var serie = new Serie { Id = serieId, Title = "The Office", SortTitle = "Office, The" };
        var season = new SerieSeason
        {
            Id = seasonId,
            SerieId = serieId,
            Serie = serie,
            SeasonNumber = 7,
            Title = "Season 7",
            SortTitle = "Season 7"
        };
        serie.Seasons.Add(season);
        var episode = new SerieEpisode
        {
            Id = episodeId,
            SerieId = serieId,
            Serie = serie,
            SeasonId = seasonId,
            Season = season,
            EpisodeNumber = 25,
            Title = "Search Committee",
            SortTitle = "Search Committee"
        };
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episodeId));
        _context.Medias.AddRange(serie, season, episode);
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "office-double",
                    MediaType = "episode",
                    Title = "Recherche directeur desesperement - Partie 2",
                    SeriesTitle = "The Office (US)",
                    SeasonNumber = 7,
                    EpisodeNumber = 25
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episodeId);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchCombinedPartTwo_WhenEpisodeExistsWithoutFile()
    {
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episode25Id = Guid.NewGuid();
        var episode26Id = Guid.NewGuid();
        var serie = new Serie { Id = serieId, Title = "The Office", SortTitle = "Office, The" };
        var season = new SerieSeason
        {
            Id = seasonId,
            SerieId = serieId,
            Serie = serie,
            SeasonNumber = 7,
            Title = "Season 7",
            SortTitle = "Season 7"
        };
        serie.Seasons.Add(season);
        var episode25 = new SerieEpisode
        {
            Id = episode25Id,
            SerieId = serieId,
            Serie = serie,
            SeasonId = seasonId,
            Season = season,
            EpisodeNumber = 25,
            Title = "Search Committee",
            SortTitle = "Search Committee"
        };
        episode25.IndexedFiles.Add(CreateIndexedFile(SeedLibrary(LibraryMediaType.Serie), episode25Id));
        var episode26 = new SerieEpisode
        {
            Id = episode26Id,
            SerieId = serieId,
            Serie = serie,
            SeasonId = seasonId,
            Season = season,
            EpisodeNumber = 26,
            Title = "Search Committee",
            SortTitle = "Search Committee"
        };
        _context.Medias.AddRange(serie, season, episode25, episode26);
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "office-part2",
                    MediaType = "episode",
                    Title = "Recherche directeur desesperement - Partie 2",
                    SeriesTitle = "The Office (US)",
                    SeasonNumber = 7,
                    EpisodeNumber = 26,
                    EpisodeNumberEnd = 26
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode26Id);
    }

    [Test]
    public async Task Handle_ShouldNotMapCombinedPartTwo_WhenLaterEpisodeDoesNotExist()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episode25Id = Guid.NewGuid();
        var serie = new Serie { Id = serieId, Title = "The Office", SortTitle = "Office, The" };
        var season = new SerieSeason
        {
            Id = seasonId,
            SerieId = serieId,
            Serie = serie,
            SeasonNumber = 7,
            Title = "Season 7",
            SortTitle = "Season 7"
        };
        serie.Seasons.Add(season);
        var episode25 = new SerieEpisode
        {
            Id = episode25Id,
            SerieId = serieId,
            Serie = serie,
            SeasonId = seasonId,
            Season = season,
            EpisodeNumber = 25,
            Title = "Search Committee",
            SortTitle = "Search Committee"
        };
        episode25.IndexedFiles.Add(CreateIndexedFile(libraryId, episode25Id));
        _context.Medias.AddRange(serie, season, episode25);
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "office-part2-no-fallback",
                    MediaType = "episode",
                    Title = "Recherche directeur desesperement - Partie 2",
                    SeriesTitle = "The Office (US)",
                    SeasonNumber = 7,
                    EpisodeNumber = 26,
                    EpisodeNumberEnd = 26
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesTitleDiffersByColonSpacing()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("Demon Slayer: Kimetsu no Yaiba", season: 2, episode: 5, "A l'avant du train");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "ds-colon",
                    MediaType = "episode",
                    Title = "A l'avant du train",
                    SeriesTitle = "Demon Slayer : Kimetsu no Yaiba",
                    SeasonNumber = 2,
                    EpisodeNumber = 5
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesTitleUsesCommaInsteadOfColon()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("90210 Beverly Hills, Nouvelle Generation", season: 1, episode: 1, "Bienvenue a Beverly Hills");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "90210-s01e01",
                    MediaType = "episode",
                    Title = "Bienvenue a Beverly Hills",
                    SeriesTitle = "90210 Beverly Hills : Nouvelle generation",
                    SeasonNumber = 1,
                    EpisodeNumber = 1,
                    Year = 2008
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesTitleOmitsUniqueYearSuffix()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("Hunter x Hunter (2011)", season: 1, episode: 9, "Attention aux prisonniers");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "hxh",
                    MediaType = "episode",
                    Title = "Attention aux prisonniers",
                    SeriesTitle = "Hunter x Hunter",
                    SeasonNumber = 1,
                    EpisodeNumber = 9
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchAnimeOnePiece_WhenLiveActionYearSuffixedShowAlsoExists()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var animeEpisode = SeedEpisode("One Piece", season: 1, episode: 1, "Je suis Luffy");
        var liveEpisode = SeedEpisode("One Piece (2023)", season: 1, episode: 1, "Romance Dawn");
        animeEpisode.IndexedFiles.Add(CreateIndexedFile(libraryId, animeEpisode.Id));
        liveEpisode.IndexedFiles.Add(CreateIndexedFile(libraryId, liveEpisode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "op",
                    MediaType = "episode",
                    Title = "Je suis Luffy",
                    SeriesTitle = "One Piece",
                    SeasonNumber = 1,
                    EpisodeNumber = 1
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(animeEpisode.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesTitleUsesFractionSlash()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("Fate\u2044Extra: Last Encore", season: 1, episode: 1, "Aujourd'hui");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "fate",
                    MediaType = "episode",
                    Title = "Aujourd'hui, le fond des anciennes limbes",
                    SeriesTitle = "Fate/Extra: Last Encore",
                    SeasonNumber = 1,
                    EpisodeNumber = 1
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesTitleDropsTrailingPeriod()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("bref.", season: 2, episode: 1, "Bref. Tout redemarre.");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "bref",
                    MediaType = "episode",
                    Title = "Bref. Tout redemarre.",
                    SeriesTitle = "Bref",
                    SeasonNumber = 2,
                    EpisodeNumber = 1
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesTitleOmitsPresents()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode(
            "The Boys presentent - Les Diaboliques",
            season: 1,
            episode: 1,
            "Laser Baby",
            seriesOriginalTitle: "The Boys Presents: Diabolical");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "boys",
                    MediaType = "episode",
                    Title = "Laser Baby",
                    SeriesTitle = "The Boys: Diabolical",
                    SeasonNumber = 1,
                    EpisodeNumber = 1
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenBungouRomanizationDiffers()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("Bungo Stray Dogs", season: 1, episode: 11, "Ce travail");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "bsd",
                    MediaType = "episode",
                    Title = "Ce travail n'est pas fait pour elle / L'agence de la joie",
                    SeriesTitle = "Bungou Stray Dogs",
                    SeasonNumber = 1,
                    EpisodeNumber = 11
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchKonosubaEpisode_WhenShortNameIsSharedAndSeasonIsUnique()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var main = SeedEpisode(
            "Konosuba -God's blessing on this wonderful world!", season: 3, episode: 6, "A Farewell");
        var spinOff = SeedEpisode(
            "KonoSuba - An Explosion on This Wonderful World!", season: 1, episode: 6, "Explosion");
        main.IndexedFiles.Add(CreateIndexedFile(libraryId, main.Id));
        spinOff.IndexedFiles.Add(CreateIndexedFile(libraryId, spinOff.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "kono",
                    MediaType = "episode",
                    Title = "A Farewell to This Lavish Lifestyle!",
                    SeriesTitle = "Konosuba : Sois Beni Monde Merveilleux !",
                    SeasonNumber = 3,
                    EpisodeNumber = 6
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(main.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchRankingOfKingsSpinOff_WhenFullTitleIsUniqueEvenIfSeasonOverlaps()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var main = SeedEpisode("Ranking of Kings", season: 1, episode: 1, "The Boy of Bojji");
        var spinOff = SeedEpisode(
            "Ranking of Kings : Le tr\u00e9sor du courage", season: 1, episode: 1, "Les douze travaux d'Ombre");
        main.IndexedFiles.Add(CreateIndexedFile(libraryId, main.Id));
        spinOff.IndexedFiles.Add(CreateIndexedFile(libraryId, spinOff.Id));
        main.Serie.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "120089" });
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "rok-courage-s01e01",
                    MediaType = "episode",
                    Title = "Les douze travaux d'Ombre",
                    SeriesTitle = "Ranking of Kings : Le tr\u00e9sor du courage",
                    SeasonNumber = 1,
                    EpisodeNumber = 1,
                    Year = 2023,
                    SeriesExternalIds = new Dictionary<string, string> { ["tmdb"] = "120089" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(spinOff.Id);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchKonosubaSpinOff_WhenFullTitleIsUniqueEvenIfSeasonOverlaps()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var main = SeedEpisode(
            "Konosuba -God's blessing on this wonderful world!", season: 1, episode: 10, "Main 10");
        var spinOff = SeedEpisode(
            "KONOSUBA \u2013 AN EXPLOSION ON THIS WONDERFUL WORLD!", season: 1, episode: 10, "Outlaws");
        main.IndexedFiles.Add(CreateIndexedFile(libraryId, main.Id));
        spinOff.IndexedFiles.Add(CreateIndexedFile(libraryId, spinOff.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "explosion-s01e10",
                    MediaType = "episode",
                    Title = "Outlaws of the Town for Beginners",
                    SeriesTitle = "KonoSuba - An Explosion on This Wonderful World!",
                    SeasonNumber = 1,
                    EpisodeNumber = 10,
                    Year = 2023
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(spinOff.Id);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldAttachCreatedEpisodeToExistingSeries_WhenTitleFolds()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var existing = SeedEpisode(
            "KONOSUBA \u2013 AN EXPLOSION ON THIS WONDERFUL WORLD!", season: 1, episode: 10, "Outlaws");
        existing.IndexedFiles.Add(CreateIndexedFile(libraryId, existing.Id));
        await _context.SaveChangesAsync();
        var serieId = existing.SerieId;

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "explosion-s01e11",
                    MediaType = "episode",
                    Title = "A later episode",
                    SeriesTitle = "KonoSuba - An Explosion on This Wonderful World!",
                    SeasonNumber = 1,
                    EpisodeNumber = 11,
                    Year = 2023
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].WasCreated.Should().BeTrue();
        (await _context.Medias.OfType<Serie>().CountAsync()).Should().Be(1);
        var created = await _context.Medias.OfType<SerieEpisode>()
            .SingleAsync(e => e.Id == response.Results[0].MediaId);
        created.SerieId.Should().Be(serieId);
        created.EpisodeNumber.Should().Be(11);
    }

    [Test]
    public async Task Handle_ShouldNotMatchDanMachiSpinOff_WhenOnlyShortNameOverlaps()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var spinOff = SeedEpisode("DanMachi: Sword Oratoria", season: 1, episode: 1, "Sword");
        var main = SeedEpisode(
            "Is It Wrong to Try to Pick Up Girls in a Dungeon?", season: 1, episode: 1, "Bell");
        spinOff.IndexedFiles.Add(CreateIndexedFile(libraryId, spinOff.Id));
        main.IndexedFiles.Add(CreateIndexedFile(libraryId, main.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "dan",
                    MediaType = "episode",
                    Title = "Bell Cranel",
                    SeriesTitle = "DanMachi - La legende des Familias",
                    SeasonNumber = 1,
                    EpisodeNumber = 1
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesExternalIdsResolveTheShow()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode(
            "Is It Wrong to Try to Pick Up Girls in a Dungeon?", season: 1, episode: 1, "Bell");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        episode.Serie.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "62745" });
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "dan",
                    MediaType = "episode",
                    Title = "Bell Cranel",
                    SeriesTitle = "DanMachi - La legende des Familias",
                    SeasonNumber = 1,
                    EpisodeNumber = 1,
                    SeriesExternalIds = new Dictionary<string, string> { ["tmdb"] = "62745" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesTitleIsFaceToFaceWithYearSuffix()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("Face \u00e0 face (2022)", season: 3, episode: 10, "Episode 10");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "face-s03e10",
                    MediaType = "episode",
                    Title = "Episode 10",
                    SeriesTitle = "Face to Face",
                    SeasonNumber = 3,
                    EpisodeNumber = 10
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchEpisode_WhenSeriesTitleIsEnglishSubtitleOnly()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("Tsugai - Daemons of the Shadow Realm", season: 1, episode: 1, "Episode 1");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "tsugai-s01e01",
                    MediaType = "episode",
                    Title = "Episode #1.1",
                    SeriesTitle = "Daemons of the Shadow Realm",
                    SeasonNumber = 1,
                    EpisodeNumber = 1,
                    Year = 2026
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchMayfairByLastToken_WhenTranslationDiffers()
    {
        var libraryId = SeedLibrary(LibraryMediaType.Serie);
        var episode = SeedEpisode("Mayfair Witches", season: 1, episode: 4, "Cette grande famille");
        episode.IndexedFiles.Add(CreateIndexedFile(libraryId, episode.Id));
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "mayfair",
                    MediaType = "episode",
                    Title = "Cette grande famille",
                    SeriesTitle = "Sorcieres de Mayfair",
                    SeasonNumber = 1,
                    EpisodeNumber = 4
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(episode.Id);
    }

    [Test]
    public async Task Handle_ShouldMatchMusic_WhenK7ArtistUsesJapaneseNameAndMusicBrainzSortName()
    {
        var trackId = SeedPlayableTrack(
            "Hyrule Field Main Theme",
            "近藤浩治",
            "ゼルダの伝説 時のオカリナ オリジナルサウンドトラック",
            artistSortTitle: "Kondo, Koji");

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "zelda-field",
                    MediaType = "music",
                    Title = "Hyrule Field Main Theme",
                    ArtistName = "Koji Kondo",
                    AlbumName = "The Legend of Zelda: Ocarina of Time Original Soundtrack",
                    Year = 1998
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(trackId);
        response.Results[0].WasCreated.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldMatchMusic_WhenSourceArtistMatchesAlbumPrefix()
    {
        var trackId = SeedPlayableTrack(
            "Blood Sweat & Tears",
            "Sheryl Lee Ralph",
            "Arcane: League of Legends: Season Two Original Soundtrack");

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "arcane",
                    MediaType = "music",
                    Title = "Blood Sweat & Tears",
                    ArtistName = "Arcane",
                    AlbumName = "Arcane"
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().ContainSingle();
        response.Results[0].MediaId.Should().Be(trackId);
    }

    [Test]
    public async Task Handle_ShouldIgnoreSeriesExternalId_WhenItemIsAnEpisode()
    {
        var serieId = Guid.NewGuid();
        var serie = new Serie { Id = serieId, Title = "Peaky Blinders", SortTitle = "Peaky Blinders" };
        serie.ExternalIds.Add(new ExternalId { ProviderName = "tvdb", Value = "270915" });
        _context.Medias.Add(serie);
        await _context.SaveChangesAsync();

        var response = await _handler.Handle(new BulkCreateMediasCommand
        {
            CreateMissing = false,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "haikyuu",
                    MediaType = "episode",
                    Title = "Admiration",
                    SeriesTitle = "Haikyuu!!",
                    SeasonNumber = 1,
                    EpisodeNumber = 10,
                    ExternalIds = new Dictionary<string, string> { ["tvdb"] = "270915" }
                }
            ]
        }, CancellationToken.None);

        response.Results.Should().BeEmpty();
        (await _context.Medias.OfType<Serie>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldQueueMetadataRefresh_WhenFetchMetadataAndProviderSupported()
    {
        var provider = Substitute.For<IMetadataProviderInfo>();
        provider.ProviderName.Returns("tmdb");
        _handler = new BulkCreateMediasCommandHandler(_context, _sender, [provider], new MediaIdentityLookupService(_context));

        await _handler.Handle(new BulkCreateMediasCommand
        {
            FetchMetadata = true,
            Items =
            [
                new BulkCreateMediasRequest.BulkCreateMediaItem
                {
                    Key = "m1",
                    MediaType = "movie",
                    Title = "Fresh",
                    ExternalIds = new Dictionary<string, string> { ["tmdb"] = "7" }
                }
            ]
        }, CancellationToken.None);

        await _sender.Received(1).Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>());
    }

    private SerieEpisode SeedEpisode(
        string seriesTitle,
        int season,
        int episode,
        string episodeTitle,
        string? seriesOriginalTitle = null)
    {
        var serieId = Guid.NewGuid();
        var seasonId = Guid.NewGuid();
        var episodeId = Guid.NewGuid();
        var serie = new Serie
        {
            Id = serieId,
            Title = seriesTitle,
            SortTitle = seriesTitle,
            OriginalTitle = seriesOriginalTitle
        };
        var seasonEntity = new SerieSeason
        {
            Id = seasonId,
            SerieId = serieId,
            Serie = serie,
            SeasonNumber = season,
            Title = $"Season {season}",
            SortTitle = $"Season {season}"
        };
        serie.Seasons.Add(seasonEntity);
        var episodeEntity = new SerieEpisode
        {
            Id = episodeId,
            SerieId = serieId,
            Serie = serie,
            SeasonId = seasonId,
            Season = seasonEntity,
            EpisodeNumber = episode,
            Title = episodeTitle,
            SortTitle = episodeTitle
        };
        _context.Medias.AddRange(serie, seasonEntity, episodeEntity);
        return episodeEntity;
    }

    private Guid SeedVirtualTrack(string title, string artistName, string albumName)
    {
        var artistId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var artist = new MusicArtist { Id = artistId, Title = artistName, SortTitle = artistName };
        var album = new MusicAlbum { Id = albumId, Title = albumName, SortTitle = albumName, ArtistId = artistId, Artist = artist };
        var track = new MusicTrack
        {
            Id = trackId,
            Title = title,
            SortTitle = title,
            AlbumId = albumId,
            Album = album,
            ArtistId = artistId,
            Artist = artist
        };
        _context.Medias.AddRange(artist, album, track);
        _context.SaveChanges();
        return trackId;
    }

    private Guid SeedPlayableTrack(string title, string artistName, string albumName, string? artistSortTitle = null)
    {
        var libraryId = SeedLibrary(LibraryMediaType.Music);
        var artistId = Guid.NewGuid();
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var artist = new MusicArtist { Id = artistId, Title = artistName, SortTitle = artistSortTitle ?? artistName };
        var album = new MusicAlbum { Id = albumId, Title = albumName, SortTitle = albumName, ArtistId = artistId, Artist = artist };
        var track = new MusicTrack
        {
            Id = trackId,
            Title = title,
            SortTitle = title,
            AlbumId = albumId,
            Album = album,
            ArtistId = artistId,
            Artist = artist
        };
        track.IndexedFiles.Add(CreateIndexedFile(libraryId, trackId));
        _context.Medias.AddRange(artist, album, track);
        _context.SaveChanges();
        return trackId;
    }

    private Guid SeedLibrary(LibraryMediaType mediaType)
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Group",
            MediaType = mediaType
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Lib",
            MediaType = mediaType,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        return libraryId;
    }

    private static IndexedFile CreateIndexedFile(Guid libraryId, Guid mediaId) => new()
    {
        Id = Guid.NewGuid(),
        LibraryId = libraryId,
        MediaId = mediaId,
        Name = "file",
        Extension = ".mkv",
        Path = $"/media/{mediaId}.mkv",
        Hash = 1,
        Size = 10
    };
}
