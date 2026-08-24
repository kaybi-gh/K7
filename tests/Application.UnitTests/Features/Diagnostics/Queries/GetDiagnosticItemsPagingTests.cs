using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticItems;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Features.Diagnostics.Queries;

[TestFixture]
public class GetDiagnosticItemsPagingTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private GetDiagnosticItemsQueryHandler _handler = null!;
    private Guid _libraryId;
    private Guid _libraryGroupId;

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
        _handler = new GetDiagnosticItemsQueryHandler(_context, Options.Create(new PathsConfiguration()));
        SeedLibrary();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnMissingExternalIdPage_WhenOtherIssuesSortFirst()
    {
        // Movies with external ids but no posters sort first ("Alpha...") and would fill page 1
        // of the broad candidate query, hiding MissingExternalId medias ("Zulu...") after filter.
        for (var i = 0; i < 30; i++)
        {
            var id = AddMovie($"Alpha {i:D2}", 2020);
            AddExternalId(id, "tmdb", $"{1000 + i}");
        }

        var missing1 = AddMovie("Zulu Missing 1", 2020);
        var missing2 = AddMovie("Zulu Missing 2", 2020);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetDiagnosticItemsQuery
            {
                EntityType = DiagnosticEntityType.Media,
                Issues = [DiagnosticIssue.MissingExternalId],
                PageNumber = 1,
                PageSize = 10
            },
            CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Select(i => i.EntityId).Should().BeEquivalentTo([missing1, missing2]);
        result.Items.Should().OnlyContain(i =>
            i.Issues.Count == 1 && i.Issues[0] == DiagnosticIssue.MissingExternalId);
    }

    [Test]
    public async Task Handle_ShouldRespectLibraryFilter_ForMissingExternalId()
    {
        var otherLibraryId = Guid.NewGuid();
        _context.Libraries.Add(new Library
        {
            Id = otherLibraryId,
            LibraryGroupId = _libraryGroupId,
            Title = "Other",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/other",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        var inLibrary = AddMovie("Local Missing", 2020);
        var elsewhere = AddMovie("Elsewhere Missing", 2020, libraryId: otherLibraryId);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetDiagnosticItemsQuery
            {
                LibraryId = _libraryId,
                Issues = [DiagnosticIssue.MissingExternalId],
                PageNumber = 1,
                PageSize = 20
            },
            CancellationToken.None);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(i => i.EntityId == inLibrary);
        result.Items.Should().NotContain(i => i.EntityId == elsewhere);
    }

    [Test]
    public async Task Handle_ShouldKeepMultipleIssuesOnSameMediaRow()
    {
        // Each movie lacks pictures AND external ids => one row with both issues.
        AddMovie("Alpha Multi", 2020);
        AddMovie("Bravo Multi", 2020);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetDiagnosticItemsQuery
            {
                EntityType = DiagnosticEntityType.Media,
                PageNumber = 1,
                PageSize = 50
            },
            CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i =>
            i.Issues.Contains(DiagnosticIssue.MissingExternalId)
            && i.Issues.Contains(DiagnosticIssue.MissingPictures));
    }

    [Test]
    public async Task Handle_ShouldIncludeMusicTracksMissingAudioAnalysis_WhenLinkedOnlyViaIndexedFile()
    {
        // Tracks are typically linked through IndexedFiles, not MediaLibraryAvailability.
        // Summary counts them that way; the items query must too or the UI shows e.g. 3 albums vs 63 items.
        var musicGroupId = Guid.NewGuid();
        var musicLibraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = musicGroupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music
        });
        _context.Libraries.Add(new Library
        {
            Id = musicLibraryId,
            LibraryGroupId = musicGroupId,
            Title = "Music",
            MediaType = LibraryMediaType.Music,
            RootPath = "/music",
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            MusicAudioAnalysisEnabled = true
        });

        var albumId = Guid.NewGuid();
        _context.Medias.Add(new MusicAlbum { Id = albumId, Title = "Only Album" });
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = musicLibraryId,
            MediaId = albumId
        });

        var trackIds = new List<Guid>();
        for (var i = 1; i <= 5; i++)
        {
            var trackId = Guid.NewGuid();
            trackIds.Add(trackId);
            _context.Medias.Add(new MusicTrack
            {
                Id = trackId,
                Title = $"Track {i}",
                AlbumId = albumId,
                TrackNumber = i
            });
            _context.IndexedFiles.Add(new IndexedFile
            {
                Id = Guid.NewGuid(),
                LibraryId = musicLibraryId,
                Name = $"track{i}",
                Extension = ".flac",
                Path = $"/music/album/track{i}.flac",
                Hash = (uint)i,
                Size = 1000,
                MediaId = trackId
            });
        }

        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetDiagnosticItemsQuery
            {
                LibraryId = musicLibraryId,
                EntityType = DiagnosticEntityType.Media,
                Issues = [DiagnosticIssue.MissingAudioAnalysis],
                PageNumber = 1,
                PageSize = 50
            },
            CancellationToken.None);

        result.TotalCount.Should().Be(5);
        result.Items.Should().HaveCount(5);
        result.Items.Select(i => i.EntityId).Should().BeEquivalentTo(trackIds);
        result.Items.Should().OnlyContain(i =>
            i.Issues.Count == 1 && i.Issues[0] == DiagnosticIssue.MissingAudioAnalysis);
    }

    [Test]
    public async Task Handle_ShouldReturnMergedOrphanFileRows_WhenFilteringOrphanOrUnidentified()
    {
        var identifiedOrphanId = Guid.NewGuid();
        var unidentifiedOrphanId = Guid.NewGuid();
        var linkedIdentifiedId = Guid.NewGuid();
        var linkedUnidentifiedId = Guid.NewGuid();

        _context.IndexedFiles.AddRange(
            new IndexedFile
            {
                Id = identifiedOrphanId,
                LibraryId = _libraryId,
                Name = "identified-orphan",
                Extension = ".mkv",
                Path = "/media/identified-orphan.mkv",
                Hash = 1u,
                Size = 100,
                MediaId = null,
                Identification = new MediaIdentification("Known Title") { ReleaseYear = new DateOnly(2020, 1, 1) }
            },
            new IndexedFile
            {
                Id = unidentifiedOrphanId,
                LibraryId = _libraryId,
                Name = "unidentified-orphan",
                Extension = ".mkv",
                Path = "/media/unidentified-orphan.mkv",
                Hash = 2u,
                Size = 100,
                MediaId = null,
                Identification = null
            },
            new IndexedFile
            {
                Id = linkedIdentifiedId,
                LibraryId = _libraryId,
                Name = "linked",
                Extension = ".mkv",
                Path = "/media/linked.mkv",
                Hash = 3u,
                Size = 100,
                MediaId = AddMovie("Linked Movie", 2020),
                Identification = new MediaIdentification("Linked Movie") { ReleaseYear = new DateOnly(2020, 1, 1) }
            },
            new IndexedFile
            {
                Id = linkedUnidentifiedId,
                LibraryId = _libraryId,
                Name = "linked-unidentified",
                Extension = ".mkv",
                Path = "/media/linked-unidentified.mkv",
                Hash = 4u,
                Size = 100,
                MediaId = AddMovie("Linked Unidentified", 2021),
                Identification = null
            });
        await _context.SaveChangesAsync();

        var byOrphan = await _handler.Handle(
            new GetDiagnosticItemsQuery
            {
                EntityType = DiagnosticEntityType.IndexedFile,
                Issues = [DiagnosticIssue.OrphanFile],
                PageNumber = 1,
                PageSize = 50
            },
            CancellationToken.None);

        byOrphan.TotalCount.Should().Be(2);
        byOrphan.Items.Should().HaveCount(2);
        byOrphan.Items.Select(i => i.EntityId).Should().BeEquivalentTo([identifiedOrphanId, unidentifiedOrphanId]);
        byOrphan.Items.Select(i => i.EntityId).Should().NotContain(linkedUnidentifiedId);
        byOrphan.Items.Should().OnlyContain(i =>
            i.Issues.Count == 1 && i.Issues[0] == DiagnosticIssue.OrphanFile);
        byOrphan.Items.Should().NotContain(i => i.Issues.Contains(DiagnosticIssue.UnidentifiedFile));

        var identified = byOrphan.Items.Single(i => i.EntityId == identifiedOrphanId);
        identified.Severity.Should().Be(DiagnosticSeverity.Error);

        var unidentified = byOrphan.Items.Single(i => i.EntityId == unidentifiedOrphanId);
        unidentified.Severity.Should().Be(DiagnosticSeverity.Error);

        var byUnidentifiedAlias = await _handler.Handle(
            new GetDiagnosticItemsQuery
            {
                EntityType = DiagnosticEntityType.IndexedFile,
                Issue = DiagnosticIssue.UnidentifiedFile,
                PageNumber = 1,
                PageSize = 50
            },
            CancellationToken.None);

        byUnidentifiedAlias.TotalCount.Should().Be(2);
        byUnidentifiedAlias.Items.Select(i => i.EntityId)
            .Should().BeEquivalentTo([identifiedOrphanId, unidentifiedOrphanId]);
        byUnidentifiedAlias.Items.Should().OnlyContain(i => i.Issues[0] == DiagnosticIssue.OrphanFile);
    }

    [Test]
    public async Task Handle_ShouldNotListMissingFiles()
    {
        // Leaf media with availability but no IndexedFile used to surface MissingFiles.
        AddMovie("No File Media", 2020);
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetDiagnosticItemsQuery
            {
                EntityType = DiagnosticEntityType.Media,
                Issues = [DiagnosticIssue.MissingFiles],
                PageNumber = 1,
                PageSize = 50
            },
            CancellationToken.None);

        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    private void SeedLibrary()
    {
        _libraryId = Guid.NewGuid();
        _libraryGroupId = Guid.NewGuid();

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = _libraryGroupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = _libraryGroupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
    }

    private Guid AddMovie(string title, int year, Guid? libraryId = null)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = title,
            ReleaseDate = new DateOnly(year, 6, 15)
        };
        _context.Medias.Add(movie);
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = libraryId ?? _libraryId,
            MediaId = movie.Id
        });
        return movie.Id;
    }

    private void AddExternalId(Guid mediaId, string providerName, string value) =>
        _context.ExternalIds.Add(new ExternalId
        {
            Id = Guid.NewGuid(),
            ProviderName = providerName,
            Value = value,
            MediaId = mediaId
        });
}
