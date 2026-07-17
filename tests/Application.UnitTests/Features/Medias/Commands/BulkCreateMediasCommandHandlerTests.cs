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
