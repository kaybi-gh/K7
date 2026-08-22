using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Entities.Playlists;
using K7.Server.Domain.Entities.Ratings;
using K7.Server.Domain.Entities.Reviews;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class CreateMediaCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private IServiceProvider _serviceProvider = null!;
    private ServiceProvider _serviceProviderRoot = null!;
    private CreateMediaCommandHandler _handler = null!;
    private IMetadataProvider<ExternalMovieMetadata> _movieProvider = null!;

    private Guid _libraryId;
    private Guid _groupId;

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

        _groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = _groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = _groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        _movieProvider = Substitute.For<IMetadataProvider<ExternalMovieMetadata>>();
        _movieProvider.ProviderName.Returns("tmdb");
        _movieProvider.SearchAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("tmdb-42");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("tmdb", _movieProvider);
        _serviceProviderRoot = services.BuildServiceProvider();
        _serviceProvider = _serviceProviderRoot;

        _sender = Substitute.For<ISender>();
        var paths = Options.Create(new PathsConfiguration { Metadatas = Path.GetTempPath() });
        var tagReader = Substitute.For<IAudioTagReader>();
        var tagSync = Substitute.For<IMediaMetadataTagSyncService>();

        var availability = new MediaLibraryAvailabilityService(
            _context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        var serieIdentity = new SerieMetadataIdentityService(
            Enumerable.Empty<ISearchableMetadataProvider>(),
            _serviceProvider,
            Substitute.For<ILogger<SerieMetadataIdentityService>>());

        _handler = new CreateMediaCommandHandler(
            _context,
            _sender,
            _serviceProvider,
            tagReader,
            paths,
            tagSync,
            new MediaIdentityLookupService(_context),
            new MediaIdentityLock(),
            availability,
            serieIdentity,
            new MusicMetadataIdentityService(_serviceProvider, Substitute.For<ILogger<MusicMetadataIdentityService>>()),
            Substitute.For<IMusicIntelligenceCatalogReconciler>(),
            new PlaybackBookmarkService(_context, Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaybackBookmarkService>.Instance),
            Substitute.For<ILogger<CreateMediaCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
        _serviceProviderRoot.Dispose();
    }

    [Test]
    public async Task Handle_ShouldCreateMovieAndQueueRefresh_WhenProviderReturnsExternalId()
    {
        var indexedFile = await SeedMovieIndexedFileAsync("Inception", 2010);
        CreateBackgroundTaskCommand? capturedTask = null;
        _sender.Send(Arg.Do<CreateBackgroundTaskCommand>(c => capturedTask = c), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = _libraryId,
            IndexedFileIds = [indexedFile.Id]
        }, CancellationToken.None);

        var movie = await _context.Medias.OfType<Movie>().SingleAsync(m => m.Id == mediaId);
        movie.Title.Should().Be("Inception");
        movie.ReleaseDate.Should().Be(new DateOnly(2010, 1, 1));
        movie.ExternalIds.Should().ContainSingle(e => e.Value == "tmdb-42" && e.ProviderName == "tmdb");
        movie.IndexedFiles.Should().ContainSingle(f => f.Id == indexedFile.Id);
        (await _context.MediaLibraryAvailabilities.CountAsync(a =>
            a.LibraryId == _libraryId && a.MediaId == mediaId)).Should().Be(1);

        capturedTask.Should().NotBeNull();
        capturedTask!.Request.Should().BeOfType<RefreshMediaMetadatasCommand>();
        var refresh = (RefreshMediaMetadatasCommand)capturedTask.Request;
        refresh.MediaId.Should().Be(mediaId);
        refresh.MetadataProviderExternalId.Should().Be("tmdb-42");
    }

    [Test]
    public async Task Handle_ShouldEnsureLibraryAvailability_WhenAttachingFileToExistingMovie()
    {
        var existingId = Guid.NewGuid();
        var existing = new Movie { Id = existingId, Title = "Existing" };
        existing.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "tmdb-42" });
        _context.Medias.Add(existing);
        await _context.SaveChangesAsync();

        var indexedFile = await SeedMovieIndexedFileAsync("Different Title", 2010);

        await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = _libraryId,
            IndexedFileIds = [indexedFile.Id]
        }, CancellationToken.None);

        (await _context.MediaLibraryAvailabilities.CountAsync(a =>
            a.LibraryId == _libraryId && a.MediaId == existingId)).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldReuseExistingMovie_WhenExternalIdAlreadyExists()
    {
        var existingId = Guid.NewGuid();
        var existing = new Movie { Id = existingId, Title = "Existing" };
        existing.ExternalIds.Add(new ExternalId { ProviderName = "tmdb", Value = "tmdb-42" });
        _context.Medias.Add(existing);
        await _context.SaveChangesAsync();

        var indexedFile = await SeedMovieIndexedFileAsync("Different Title", 2010);

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = _libraryId,
            IndexedFileIds = [indexedFile.Id]
        }, CancellationToken.None);

        mediaId.Should().Be(existingId);
        (await _context.Medias.OfType<Movie>().CountAsync()).Should().Be(1);
        var attachedFile = await _context.IndexedFiles.SingleAsync(f => f.Id == indexedFile.Id);
        attachedFile.MediaId.Should().Be(existingId);
        await _sender.DidNotReceive().Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReuseMovieByTitle_WhenExternalIdMissingButTitleMatches()
    {
        var existingId = Guid.NewGuid();
        _context.Medias.Add(new Movie
        {
            Id = existingId,
            Title = "Inception",
            ReleaseDate = new DateOnly(2010, 1, 1)
        });
        await _context.SaveChangesAsync();

        var indexedFile = await SeedMovieIndexedFileAsync("Inception", 2010);

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = _libraryId,
            IndexedFileIds = [indexedFile.Id]
        }, CancellationToken.None);

        mediaId.Should().Be(existingId);
        (await _context.Medias.OfType<Movie>().CountAsync()).Should().Be(1);
    }

    [Test]
    public async Task Handle_ShouldCreateMovieFromIdentification_WhenProviderReturnsNoResult()
    {
        _movieProvider.SearchAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var indexedFile = await SeedMovieIndexedFileAsync("La Tour de controle infernale", 2016);

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = _libraryId,
            IndexedFileIds = [indexedFile.Id]
        }, CancellationToken.None);

        var movie = await _context.Medias.OfType<Movie>()
            .Include(m => m.ExternalIds)
            .Include(m => m.IndexedFiles)
            .SingleAsync(m => m.Id == mediaId);

        movie.Title.Should().Be("La Tour de controle infernale");
        movie.SortTitle.Should().NotBeNullOrEmpty();
        movie.ReleaseDate.Should().Be(new DateOnly(2016, 1, 1));
        movie.ExternalIds.Should().BeEmpty();
        movie.IndexedFiles.Should().ContainSingle(f => f.Id == indexedFile.Id);

        await _sender.DidNotReceive().Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReuseMovieByTitle_WhenProviderReturnsNoResult()
    {
        _movieProvider.SearchAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(string.Empty);

        var existingId = Guid.NewGuid();
        _context.Medias.Add(new Movie
        {
            Id = existingId,
            Title = "La Tour de controle infernale",
            ReleaseDate = new DateOnly(2016, 1, 1)
        });
        await _context.SaveChangesAsync();

        var indexedFile = await SeedMovieIndexedFileAsync("La Tour de controle infernale", 2016);

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = _libraryId,
            IndexedFileIds = [indexedFile.Id]
        }, CancellationToken.None);

        mediaId.Should().Be(existingId);
        (await _context.Medias.OfType<Movie>().CountAsync()).Should().Be(1);
        await _sender.DidNotReceive().Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldTransferUserDataUsingFormerMediaIds_WhenMediaIdAlreadyCleared()
    {
        _movieProvider.SearchAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("tmdb-correct");

        var wrongMovie = new Movie { Id = Guid.NewGuid(), Title = "Wrong Title" };
        _context.Medias.Add(wrongMovie);

        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = wrongMovie.Id,
            PlayCount = 4,
        });

        var rating = new UserRating
        {
            Id = Guid.NewGuid(),
            MediaId = wrongMovie.Id,
            UserId = userId,
            Value = 9,
            MinimumValue = 0,
            MaximumValue = 10
        };
        _context.Ratings.Add(rating);
        _context.MediaReviews.Add(new MediaReview
        {
            MediaId = wrongMovie.Id,
            UserId = userId,
            UserRatingId = rating.Id,
            Text = "Great"
        });

        var playlist = new Playlist
        {
            Id = Guid.NewGuid(),
            Title = "Later",
            UserId = userId,
            MediaType = MediaType.Movie
        };
        _context.Playlists.Add(playlist);
        _context.PlaylistItems.Add(new PlaylistItem
        {
            PlaylistId = playlist.Id,
            MediaId = wrongMovie.Id,
            Order = 0
        });

        var collection = new Collection
        {
            Id = Guid.NewGuid(),
            Title = "Favs",
            UserId = userId
        };
        _context.Collections.Add(collection);
        _context.CollectionItems.Add(new CollectionItem
        {
            CollectionId = collection.Id,
            MediaId = wrongMovie.Id,
            Order = 0
        });

        _context.MediaPlaybackSessions.Add(new MediaPlaybackSession
        {
            UserId = userId,
            MediaId = wrongMovie.Id,
            SessionId = Guid.NewGuid(),
            ReferenceId = Guid.NewGuid(),
            StartedAt = DateTime.UtcNow.AddDays(-1),
            PositionSeconds = 12,
            DurationSeconds = 100,
            WatchedDurationSeconds = 12,
            State = PlaybackState.Ended
        });
        await _context.SaveChangesAsync();

        var indexedFile = await SeedMovieIndexedFileAsync("Correct Title", 2020);
        // Rematch clears MediaId before CreateMedia; former id is only in the command map.
        var formerMap = new Dictionary<Guid, Guid> { [indexedFile.Id] = wrongMovie.Id };

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = _libraryId,
            IndexedFileIds = [indexedFile.Id],
            FormerMediaIdsByIndexedFileId = formerMap
        }, CancellationToken.None);

        mediaId.Should().NotBe(wrongMovie.Id);

        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == userId);
        state.MediaId.Should().Be(mediaId);
        state.PlayCount.Should().Be(4);

        (await _context.Ratings.OfType<UserRating>().SingleAsync()).MediaId.Should().Be(mediaId);
        (await _context.MediaReviews.SingleAsync()).MediaId.Should().Be(mediaId);
        (await _context.PlaylistItems.SingleAsync()).MediaId.Should().Be(mediaId);
        (await _context.CollectionItems.SingleAsync()).MediaId.Should().Be(mediaId);
        (await _context.MediaPlaybackSessions.SingleAsync()).MediaId.Should().Be(mediaId);

        (await _context.Medias.OfType<Movie>().AnyAsync(m => m.Id == wrongMovie.Id)).Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldLeaveUserDataOnFormerMovie_WhenMediaIdClearedWithoutFormerMap()
    {
        _movieProvider.SearchAsync(Arg.Any<MediaIdentification>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns("tmdb-correct");

        var wrongMovie = new Movie { Id = Guid.NewGuid(), Title = "Wrong Title" };
        _context.Medias.Add(wrongMovie);

        var userId = Guid.NewGuid();
        _context.Users.Add(new User { Id = userId, IdentityUserId = "u1", DisplayName = "u1" });
        _context.UserMediaStates.Add(new UserMediaState
        {
            UserId = userId,
            MediaId = wrongMovie.Id,
            PlayCount = 2,
        });
        await _context.SaveChangesAsync();

        var indexedFile = await SeedMovieIndexedFileAsync("Correct Title", 2020);

        var mediaId = await _handler.Handle(new CreateMediaCommand
        {
            MediaType = MediaType.Movie,
            LibraryId = _libraryId,
            IndexedFileIds = [indexedFile.Id]
        }, CancellationToken.None);

        mediaId.Should().NotBe(wrongMovie.Id);
        var state = await _context.UserMediaStates.SingleAsync(s => s.UserId == userId);
        state.MediaId.Should().Be(wrongMovie.Id);
        (await _context.Medias.OfType<Movie>().AnyAsync(m => m.Id == wrongMovie.Id)).Should().BeTrue();
    }

    private async Task<IndexedFile> SeedMovieIndexedFileAsync(string title, int year)
    {
        var indexedFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = _libraryId,
            Name = title,
            Extension = ".mkv",
            Path = $"/media/{title}.mkv",
            Hash = (uint)Random.Shared.Next(1, int.MaxValue),
            Size = 1,
            Identification = new MediaIdentification(title) { ReleaseYear = new DateOnly(year, 1, 1) }
        };
        _context.IndexedFiles.Add(indexedFile);
        await _context.SaveChangesAsync();
        return indexedFile;
    }
}
