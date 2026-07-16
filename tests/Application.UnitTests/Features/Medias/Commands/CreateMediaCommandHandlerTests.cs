using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.External;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
        _movieProvider.SearchAsync(Arg.Any<MediaIdentification>(), Arg.Any<CancellationToken>())
            .Returns("tmdb-42");

        var services = new ServiceCollection();
        services.AddKeyedSingleton("tmdb", _movieProvider);
        _serviceProviderRoot = services.BuildServiceProvider();
        _serviceProvider = _serviceProviderRoot;

        _sender = Substitute.For<ISender>();
        var paths = Options.Create(new PathsConfiguration { Metadatas = Path.GetTempPath() });
        var tagReader = Substitute.For<IAudioTagReader>();
        var tagSync = Substitute.For<IMediaMetadataTagSyncService>();

        _handler = new CreateMediaCommandHandler(
            _context,
            _sender,
            _serviceProvider,
            tagReader,
            paths,
            tagSync);
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
        movie.ExternalIds.Should().ContainSingle(e => e.Value == "tmdb-42" && e.ProviderName == "tmdb");
        movie.IndexedFiles.Should().ContainSingle(f => f.Id == indexedFile.Id);

        capturedTask.Should().NotBeNull();
        capturedTask!.Request.Should().BeOfType<RefreshMediaMetadatasCommand>();
        var refresh = (RefreshMediaMetadatasCommand)capturedTask.Request;
        refresh.MediaId.Should().Be(mediaId);
        refresh.MetadataProviderExternalId.Should().Be("tmdb-42");
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
