using K7.Server.Application.Common;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Medias.Commands.QueueRefreshMediaMetadata;
using K7.Server.Application.Features.Medias.Commands.RefreshMediaMetadatas;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.Medias.Commands;

[TestFixture]
public class QueueRefreshMediaMetadataCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private QueueRefreshMediaMetadataCommandHandler _handler = null!;
    private CreateBackgroundTaskCommand? _capturedTask;
    private Guid _libraryId;

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

        var groupId = Guid.NewGuid();
        _libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie,
            RootPath = "/media/series",
            MetadataProviderName = MetadataProviderNames.Auto,
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        _context.SaveChanges();

        var services = new ServiceCollection().BuildServiceProvider();
        var resolver = new MediaExternalIdResolver(
            _context,
            services,
            new MusicMetadataIdentityService(
                services,
                Substitute.For<ILogger<MusicMetadataIdentityService>>()),
            Substitute.For<ILogger<MediaExternalIdResolver>>());

        _sender = Substitute.For<ISender>();
        _capturedTask = null;
        _sender.Send(Arg.Do<CreateBackgroundTaskCommand>(c => _capturedTask = c), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));

        _handler = new QueueRefreshMediaMetadataCommandHandler(_context, _sender, resolver);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldQueueTvdbRefresh_WhenLibraryProviderIsAuto()
    {
        var serie = await SeedSerieWithTvdbIdAsync();

        await _handler.Handle(new QueueRefreshMediaMetadataCommand { MediaId = serie.Id }, CancellationToken.None);

        _capturedTask.Should().NotBeNull();
        _capturedTask!.MetadataProviderName.Should().Be(MetadataProviderNames.Tvdb);
        var refresh = _capturedTask.Request.Should().BeOfType<RefreshMediaMetadatasCommand>().Subject;
        refresh.MetadataProviderName.Should().Be(MetadataProviderNames.Tvdb);
        refresh.MetadataProviderExternalId.Should().Be("337018");
        refresh.MediaId.Should().Be(serie.Id);
    }

    private async Task<Serie> SeedSerieWithTvdbIdAsync()
    {
        var serie = new Serie
        {
            Title = "Cool Show",
            NumberingProviderName = MetadataProviderNames.Tvdb
        };
        serie.ExternalIds.Add(new ExternalId
        {
            ProviderName = MetadataProviderNames.Tvdb,
            Value = "337018"
        });

        var season = new SerieSeason
        {
            Serie = serie,
            SeasonNumber = 1,
            Title = "Season 1"
        };
        var episode = new SerieEpisode
        {
            Serie = serie,
            Season = season,
            EpisodeNumber = 1,
            Title = "Pilot",
            SortTitle = "Pilot"
        };
        season.Episodes.Add(episode);
        serie.Seasons.Add(season);

        var indexedFile = new IndexedFile
        {
            LibraryId = _libraryId,
            Name = "Cool Show - S01E01.mkv",
            Extension = ".mkv",
            Path = "/media/series/Cool Show/Season 1/Cool Show - S01E01.mkv",
            ParentDirectory = "Season 1",
            Hash = 1,
            Size = 1,
            MediaId = episode.Id
        };
        episode.IndexedFiles.Add(indexedFile);

        _context.Medias.AddRange(serie, season, episode);
        _context.IndexedFiles.Add(indexedFile);
        await _context.SaveChangesAsync();
        return serie;
    }
}
