using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Medias.Queries.GetMedias;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Queries;

[TestFixture]
public class GetMediasWithPaginationApplyFiltersTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;

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
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task ApplyFilters_ShouldExcludeSerie_WhenNoMediaLibraryAvailability()
    {
        var serie = new Serie { Id = Guid.NewGuid(), Title = "Orphan Show", SortTitle = "Orphan Show" };
        _context.Medias.Add(serie);
        await _context.SaveChangesAsync();

        var mediaTypes = new EnumHashSetQueryParam<MediaType> { MediaType.Serie };
        var request = new GetMediasWithPaginationQuery
        {
            MediaTypes = mediaTypes,
            PageNumber = 1,
            PageSize = 20
        };

        var query = GetMediasQueryHandler.ApplyFilters(
            _context,
            request,
            _context.Medias.AsQueryable(),
            userId: null);

        var ids = await query.Select(m => m.Id).ToListAsync();
        ids.Should().NotContain(serie.Id);
    }

    [Test]
    public async Task ApplyFilters_ShouldIncludeSerie_WhenMediaLibraryAvailabilityExists()
    {
        var groupId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie,
            RootPath = "/media",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        var serie = new Serie { Id = Guid.NewGuid(), Title = "Available Show", SortTitle = "Available Show" };
        _context.Medias.Add(serie);
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            LibraryId = libraryId,
            MediaId = serie.Id
        });
        await _context.SaveChangesAsync();

        var mediaTypes = new EnumHashSetQueryParam<MediaType> { MediaType.Serie };
        var request = new GetMediasWithPaginationQuery
        {
            MediaTypes = mediaTypes,
            PageNumber = 1,
            PageSize = 20
        };

        var query = GetMediasQueryHandler.ApplyFilters(
            _context,
            request,
            _context.Medias.AsQueryable(),
            userId: null);

        var ids = await query.Select(m => m.Id).ToListAsync();
        ids.Should().Contain(serie.Id);
    }

    [Test]
    public async Task ApplyFilters_ShouldIncludeSerieById_EvenWithoutAvailability()
    {
        var serie = new Serie { Id = Guid.NewGuid(), Title = "Deep Link Show", SortTitle = "Deep Link Show" };
        _context.Medias.Add(serie);
        await _context.SaveChangesAsync();

        var request = new GetMediasWithPaginationQuery
        {
            Ids = [serie.Id],
            PageNumber = 1,
            PageSize = 20
        };

        var query = GetMediasQueryHandler.ApplyFilters(
            _context,
            request,
            _context.Medias.AsQueryable(),
            userId: null);

        var ids = await query.Select(m => m.Id).ToListAsync();
        ids.Should().Contain(serie.Id);
    }
}
