using K7.Server.Application.Common.Models;
using K7.Server.Application.Features.Medias.Queries.GetMediaTags;
using K7.Server.Application.Features.Medias.Services;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class GetMediaTagsTests
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
        SeedData();
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_GenreKind_ShouldReturnGenresFromMetadataTags()
    {
        var kinds = new EnumHashSetQueryParam<MetadataTagKind>();
        kinds.Add(MetadataTagKind.Genre);

        var handler = new GetMediaTagsQueryHandler(_context, new TestUser());
        var query = new GetMediaTagsQuery
        {
            Kinds = kinds,
            Limit = 20
        };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Kinds.Should().ContainSingle(k => k.Kind == MetadataTagKind.Genre);
        result.Kinds.Single(k => k.Kind == MetadataTagKind.Genre).Values
            .Should().ContainSingle(v => v.DisplayName == "Rock");
    }

    [Test]
    public async Task Handle_StudioKind_ShouldBeCaseInsensitive()
    {
        var kinds = new EnumHashSetQueryParam<MetadataTagKind>();
        kinds.Add(MetadataTagKind.Studio);

        var handler = new GetMediaTagsQueryHandler(_context, new TestUser());
        var query = new GetMediaTagsQuery
        {
            Kinds = kinds,
            SearchText = "warner",
            Limit = 20
        };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Kinds.Should().ContainSingle(k => k.Kind == MetadataTagKind.Studio);
        result.Kinds.Single(k => k.Kind == MetadataTagKind.Studio).Values
            .Should().ContainSingle(v => v.DisplayName == "Warner Bros.");
    }

    [Test]
    public async Task Handle_StudioKind_ShouldReturnDistinctMatches()
    {
        var kinds = new EnumHashSetQueryParam<MetadataTagKind>();
        kinds.Add(MetadataTagKind.Studio);

        var handler = new GetMediaTagsQueryHandler(_context, new TestUser());
        var query = new GetMediaTagsQuery
        {
            Kinds = kinds,
            SearchText = "Warner",
            Limit = 20
        };

        var result = await handler.Handle(query, CancellationToken.None);

        result.Kinds.Should().ContainSingle(k => k.Kind == MetadataTagKind.Studio);
        result.Kinds.Single(k => k.Kind == MetadataTagKind.Studio).Values
            .Should().ContainSingle(v => v.DisplayName == "Warner Bros.");
    }

    private void SeedData()
    {
        var now = DateTimeOffset.UtcNow;
        var libraryId = Guid.NewGuid();
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            Created = now,
            LastModified = now
        };

        var studioTag = new MetadataTag
        {
            Id = Guid.NewGuid(),
            Kind = MetadataTagKind.Studio,
            NormalizedKey = MetadataTagNormalizer.NormalizeKey("Warner Bros."),
            DisplayName = "Warner Bros."
        };

        var genreTag = new MetadataTag
        {
            Id = Guid.NewGuid(),
            Kind = MetadataTagKind.Genre,
            NormalizedKey = MetadataTagNormalizer.NormalizeKey("Rock"),
            DisplayName = "Rock"
        };

        var libraryGroupId = Guid.NewGuid();
        var library = new Library
        {
            Id = libraryId,
            LibraryGroupId = libraryGroupId,
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            Title = "Films",
            Created = now,
            LastModified = now
        };
        var indexedFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            Name = "inception.mkv",
            Extension = ".mkv",
            Path = "/movies/inception.mkv",
            Hash = 1,
            Size = 1,
            MediaId = movie.Id,
            Media = movie,
            Created = now,
            LastModified = now
        };
        movie.IndexedFiles.Add(indexedFile);

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = libraryGroupId,
            Title = "Films",
            MediaType = LibraryMediaType.Movie,
            Created = now,
            LastModified = now
        });
        _context.Libraries.Add(library);
        _context.Medias.Add(movie);
        _context.MetadataTags.Add(studioTag);
        _context.MetadataTags.Add(genreTag);
        _context.MediaMetadataTags.Add(new MediaMetadataTag
        {
            MediaId = movie.Id,
            Media = movie,
            MetadataTagId = studioTag.Id,
            MetadataTag = studioTag
        });
        _context.MediaMetadataTags.Add(new MediaMetadataTag
        {
            MediaId = movie.Id,
            Media = movie,
            MetadataTagId = genreTag.Id,
            MetadataTag = genreTag
        });
        _context.SaveChanges();
    }

    private sealed class TestUser : K7.Server.Application.Common.Interfaces.IUser
    {
        public string? IdentityId => null;
        public Guid? Id => null;
    }
}
