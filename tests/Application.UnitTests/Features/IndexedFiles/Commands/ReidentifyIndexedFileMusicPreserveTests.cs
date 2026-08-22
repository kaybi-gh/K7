using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.IndexedFiles.Commands.ReidentifyIndexedFile;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Application.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.UnitTests.Features.IndexedFiles.Commands;

[TestFixture]
public class ReidentifyIndexedFileMusicPreserveTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private ISender _sender = null!;
    private IMusicIntelligenceCatalogReconciler _reconciler = null!;
    private ReidentifyIndexedFileCommandHandler _handler = null!;
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
            Title = "Music",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music,
            RootPath = "/music",
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "en",
            MetadataFallbackLanguage = "en",
            MusicAudioAnalysisEnabled = false
        });
        _context.SaveChanges();

        _sender = Substitute.For<ISender>();
        _sender.Send(Arg.Any<CreateBackgroundTaskCommand>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Guid.NewGuid()));
        _reconciler = Substitute.For<IMusicIntelligenceCatalogReconciler>();

        var availability = new MediaLibraryAvailabilityService(
            _context,
            Substitute.For<IMediaQueryCacheInvalidator>(),
            Substitute.For<ILogger<MediaLibraryAvailabilityService>>());

        _handler = new ReidentifyIndexedFileCommandHandler(
            _context,
            _sender,
            availability,
            _reconciler,
            new PlaybackBookmarkService(_context, Microsoft.Extensions.Logging.Abstractions.NullLogger<PlaybackBookmarkService>.Instance),
            Substitute.For<ILogger<ReidentifyIndexedFileCommandHandler>>());
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldUpdateExternalIdInPlace_WhenTargetIdIsFree()
    {
        var (albumId, trackId, fileId) = await SeedAlbumTrackFileAsync(externalId: "mbid-wrong");

        await _handler.Handle(new ReidentifyIndexedFileCommand
        {
            IndexedFileId = fileId,
            SelectedProvider = "musicbrainz",
            SelectedExternalId = "mbid-correct"
        }, CancellationToken.None);

        var album = await _context.Medias.OfType<MusicAlbum>()
            .Include(a => a.ExternalIds)
            .SingleAsync(a => a.Id == albumId);
        var track = await _context.Medias.OfType<MusicTrack>().SingleAsync(t => t.Id == trackId);
        var file = await _context.IndexedFiles.SingleAsync(f => f.Id == fileId);

        album.ExternalIds.Should().ContainSingle(e => e.ProviderName == "musicbrainz" && e.Value == "mbid-correct");
        track.Id.Should().Be(trackId);
        file.MediaId.Should().Be(trackId);
        _reconciler.DidNotReceive().RequestReconcile();
    }

    [Test]
    public async Task Handle_ShouldMergeOntoExistingAlbumAndReconcile_WhenTargetIdAlreadyUsed()
    {
        var (wrongAlbumId, wrongTrackId, fileId) = await SeedAlbumTrackFileAsync(externalId: "mbid-wrong");
        var correctAlbumId = Guid.NewGuid();
        var correctAlbum = new MusicAlbum { Id = correctAlbumId, Title = "Correct" };
        correctAlbum.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz", Value = "mbid-correct" });
        _context.Medias.Add(correctAlbum);
        await _context.SaveChangesAsync();

        await _handler.Handle(new ReidentifyIndexedFileCommand
        {
            IndexedFileId = fileId,
            SelectedProvider = "musicbrainz",
            SelectedExternalId = "mbid-correct"
        }, CancellationToken.None);

        var file = await _context.IndexedFiles.SingleAsync(f => f.Id == fileId);
        file.MediaId.Should().NotBeNull();
        var attachedTrack = await _context.Medias.OfType<MusicTrack>().SingleAsync(t => t.Id == file.MediaId);
        attachedTrack.AlbumId.Should().Be(correctAlbumId);

        (await _context.Medias.OfType<MusicTrack>().AnyAsync(t => t.Id == wrongTrackId)).Should().BeFalse();
        (await _context.Medias.OfType<MusicAlbum>().AnyAsync(a => a.Id == wrongAlbumId)).Should().BeFalse();
        _reconciler.Received(1).RequestReconcile();
    }

    private async Task<(Guid AlbumId, Guid TrackId, Guid FileId)> SeedAlbumTrackFileAsync(string externalId)
    {
        var albumId = Guid.NewGuid();
        var trackId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var album = new MusicAlbum { Id = albumId, Title = "Album" };
        album.ExternalIds.Add(new ExternalId { ProviderName = "musicbrainz", Value = externalId });
        var track = new MusicTrack
        {
            Id = trackId,
            Title = "Song",
            TrackNumber = 1,
            AlbumId = albumId,
            Album = album
        };
        _context.Medias.Add(album);
        _context.Medias.Add(track);
        _context.IndexedFiles.Add(new IndexedFile
        {
            Id = fileId,
            LibraryId = _libraryId,
            Path = "/music/Album/01 - Song.flac",
            Name = "01 - Song.flac",
            Extension = ".flac",
            Hash = 1,
            Size = 1,
            MediaId = trackId,
            Identification = new Domain.Models.MediaIdentification("Song")
            {
                Title = "Song",
                AlbumName = "Album",
                TrackNumber = 1
            }
        });
        await _context.SaveChangesAsync();
        return (albumId, trackId, fileId);
    }
}
