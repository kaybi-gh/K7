using K7.Server.Application.Features.Medias.Queries.GetMediaBrowseFilterSuggestions;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Medias.Services;

[TestFixture]
public class GetMediaBrowseFilterSuggestionsTests
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
    public async Task Handle_ActorNameSearch_ShouldReturnDistinctMatches()
    {
        var handler = new GetMediaBrowseFilterSuggestionsQueryHandler(_context, new TestUser());
        var query = new GetMediaBrowseFilterSuggestionsQuery
        {
            Field = nameof(SmartPlaylistField.ActorName),
            SearchText = "DiCaprio",
            Limit = 20
        };

        var results = await handler.Handle(query, CancellationToken.None);

        results.Should().BeEquivalentTo(["Leonardo DiCaprio"]);
    }

    [Test]
    public async Task Handle_ActorNameSearch_ShouldBeCaseInsensitive()
    {
        var handler = new GetMediaBrowseFilterSuggestionsQueryHandler(_context, new TestUser());
        var query = new GetMediaBrowseFilterSuggestionsQuery
        {
            Field = nameof(SmartPlaylistField.ActorName),
            SearchText = "dicaprio",
            Limit = 20
        };

        var results = await handler.Handle(query, CancellationToken.None);

        results.Should().BeEquivalentTo(["Leonardo DiCaprio"]);
    }

    [Test]
    public async Task Handle_ArtistNameSearch_ShouldReturnDistinctMatches()
    {
        var handler = new GetMediaBrowseFilterSuggestionsQueryHandler(_context, new TestUser());
        var query = new GetMediaBrowseFilterSuggestionsQuery
        {
            Field = nameof(SmartPlaylistField.ArtistName),
            SearchText = "Radio",
            Limit = 20
        };

        var results = await handler.Handle(query, CancellationToken.None);

        results.Should().BeEquivalentTo(["Radiohead"]);
    }

    [Test]
    public async Task Handle_EmptySearch_ShouldReturnTopSuggestions()
    {
        var handler = new GetMediaBrowseFilterSuggestionsQueryHandler(_context, new TestUser());
        var query = new GetMediaBrowseFilterSuggestionsQuery
        {
            Field = nameof(SmartPlaylistField.ActorName),
            SearchText = "",
            Limit = 20
        };

        var results = await handler.Handle(query, CancellationToken.None);

        results.Should().Contain("Leonardo DiCaprio");
    }

    private void SeedData()
    {
        var now = DateTimeOffset.UtcNow;
        var libraryId = Guid.NewGuid();
        var person = new Person { Id = Guid.NewGuid(), Name = "Leonardo DiCaprio", Created = now, LastModified = now };
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            Created = now,
            LastModified = now
        };
        var role = new Actor
        {
            Id = Guid.NewGuid(),
            PersonId = person.Id,
            Person = person,
            MediaId = movie.Id,
            Media = movie,
            CharacterName = "Cobb",
            Created = now,
            LastModified = now
        };
        movie.PersonRoles.Add(role);
        person.Roles.Add(role);

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
        _context.Persons.Add(person);
        _context.Medias.Add(movie);
        _context.MediaLibraryAvailabilities.Add(new MediaLibraryAvailability
        {
            MediaId = movie.Id,
            LibraryId = libraryId
        });

        var artist = new MusicArtist
        {
            Id = Guid.NewGuid(),
            Title = "Radiohead",
            Created = now,
            LastModified = now
        };
        var album = new MusicAlbum
        {
            Id = Guid.NewGuid(),
            Title = "OK Computer",
            ArtistId = artist.Id,
            Artist = artist,
            Created = now,
            LastModified = now
        };
        artist.Albums.Add(album);

        var musicLibrary = new Library
        {
            Id = Guid.NewGuid(),
            LibraryGroupId = Guid.NewGuid(),
            MediaType = LibraryMediaType.Music,
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            Title = "Musique",
            Created = now,
            LastModified = now
        };
        var musicIndexedFile = new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = musicLibrary.Id,
            Name = "ok-computer.flac",
            Extension = ".flac",
            Path = "/music/ok-computer.flac",
            Hash = 2,
            Size = 1,
            MediaId = album.Id,
            Media = album,
            Created = now,
            LastModified = now
        };
        album.IndexedFiles.Add(musicIndexedFile);

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = musicLibrary.LibraryGroupId,
            Title = "Musique",
            MediaType = LibraryMediaType.Music,
            Created = now,
            LastModified = now
        });
        _context.Libraries.Add(musicLibrary);
        _context.Medias.Add(artist);
        _context.Medias.Add(album);
        _context.SaveChanges();
    }

    private sealed class TestUser : K7.Server.Application.Common.Interfaces.IUser
    {
        public string? IdentityId => null;
        public Guid? Id => null;
        public Task<Guid?> GetIdAsync(CancellationToken cancellationToken = default) => Task.FromResult(Id);
    }
}
