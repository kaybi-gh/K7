using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticItems;
using K7.Server.Application.Features.Diagnostics.Queries.GetDiagnosticsSummary;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Features.Diagnostics.Queries;

/// <summary>
/// Duplicate media detection is signal-only: media creation is a find-or-create over a fuzzy
/// identity, so duplicates cannot be prevented by construction (see DuplicateMediaDiagnosticHelper).
/// These tests run against Sqlite in-memory, which also validates that the detection queries are
/// translatable to SQL.
/// </summary>
[TestFixture]
public class DuplicateMediaDiagnosticsTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private GetDiagnosticItemsQueryHandler _itemsHandler = null!;
    private GetDiagnosticsSummaryQueryHandler _summaryHandler = null!;

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

        var paths = Options.Create(new PathsConfiguration());
        _itemsHandler = new GetDiagnosticItemsQueryHandler(_context, paths);
        _summaryHandler = new GetDiagnosticsSummaryQueryHandler(_context, paths);

        SeedLibrary();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReportDuplicateExternalId_WhenTwoMediasShareExternalId()
    {
        var first = AddMovie("Inception", 2010);
        var second = AddMovie("Inception (copy)", 2010);
        AddExternalId(first, "tmdb", "27205");
        AddExternalId(second, "tmdb", "27205");
        await _context.SaveChangesAsync();

        var items = await QueryItemsAsync(DiagnosticIssue.DuplicateExternalId);

        items.Should().HaveCount(2);
        items.Select(i => i.EntityId).Should().BeEquivalentTo([first, second]);
        items.Should().OnlyContain(i => i.Severity == DiagnosticSeverity.Warning);
    }

    [Test]
    public async Task Handle_ShouldNotReportDuplicateExternalId_WhenMediasBelongToDifferentPeers()
    {
        var peer = PeerServer.CreatePending("peer", "https://peer.example", "token");
        _context.PeerServers.Add(peer);

        var local = AddMovie("Inception", 2010);
        var federated = AddMovie("Inception", 2010, peerServerId: peer.Id, withAvailability: false);
        AddExternalId(local, "tmdb", "27205");
        AddExternalId(federated, "tmdb", "27205");
        await _context.SaveChangesAsync();

        var items = await QueryItemsAsync(DiagnosticIssue.DuplicateExternalId);

        items.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldReportSuspectedDuplicate_WhenSameTitleAndYearInSameLibrary()
    {
        // Different raw casing and whitespace: normalization is trim + case-insensitive.
        var first = AddMovie("  Inception ", 2010);
        var second = AddMovie("inception", 2010);
        await _context.SaveChangesAsync();

        var items = await QueryItemsAsync(DiagnosticIssue.SuspectedDuplicateMedia);

        items.Should().HaveCount(2);
        items.Select(i => i.EntityId).Should().BeEquivalentTo([first, second]);
        items.Should().OnlyContain(i => i.Severity == DiagnosticSeverity.Info);
    }

    [Test]
    public async Task Handle_ShouldNotReportSuspectedDuplicate_WhenYearsDiffer()
    {
        AddMovie("Dune", 1984);
        AddMovie("Dune", 2021);
        await _context.SaveChangesAsync();

        var items = await QueryItemsAsync(DiagnosticIssue.SuspectedDuplicateMedia);

        items.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldReportNothing_WhenSingleMedia()
    {
        var movie = AddMovie("Inception", 2010);
        AddExternalId(movie, "tmdb", "27205");
        await _context.SaveChangesAsync();

        var duplicateItems = await QueryItemsAsync(DiagnosticIssue.DuplicateExternalId);
        var suspectedItems = await QueryItemsAsync(DiagnosticIssue.SuspectedDuplicateMedia);

        duplicateItems.Should().BeEmpty();
        suspectedItems.Should().BeEmpty();
    }

    [Test]
    public async Task Handle_ShouldReflectDuplicateCountsInSummary()
    {
        var first = AddMovie("Inception", 2010);
        var second = AddMovie("Inception (copy)", 2010);
        AddExternalId(first, "tmdb", "27205");
        AddExternalId(second, "tmdb", "27205");
        AddMovie("Dune", 2021);
        AddMovie("Dune", 2021);
        await _context.SaveChangesAsync();

        var summaries = await _summaryHandler.Handle(new GetDiagnosticsSummaryQuery(), CancellationToken.None);

        var summary = summaries.Should().ContainSingle(s => s.LibraryId == _libraryId).Subject;
        summary.DuplicateExternalIdCount.Should().Be(2);
        summary.SuspectedDuplicateMediaCount.Should().Be(2);
    }

    private async Task<List<DiagnosticItemDto>> QueryItemsAsync(DiagnosticIssue issue)
    {
        var result = await _itemsHandler.Handle(
            new GetDiagnosticItemsQuery
            {
                EntityType = DiagnosticEntityType.Media,
                Issue = issue,
                PageNumber = 1,
                PageSize = 50
            },
            CancellationToken.None);

        return result.Items.ToList();
    }

    private void SeedLibrary()
    {
        _libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = _libraryId,
            LibraryGroupId = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
    }

    private Guid AddMovie(string title, int year, Guid? peerServerId = null, bool withAvailability = true)
    {
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = title,
            ReleaseDate = new DateOnly(year, 6, 15),
            PeerServerId = peerServerId
        };
        _context.Medias.Add(movie);

        if (withAvailability)
        {
            _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
            {
                LibraryId = _libraryId,
                MediaId = movie.Id
            });
        }

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
