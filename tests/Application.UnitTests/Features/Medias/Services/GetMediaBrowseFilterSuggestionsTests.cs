using K7.Server.Application.Features.Medias.Queries.GetMediaBrowseFilterSuggestions;
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
    public async Task Handle_StudioSearch_ShouldReturnDistinctMatches()
    {
        var handler = new GetMediaBrowseFilterSuggestionsQueryHandler(_context, new TestUser());
        var query = new GetMediaBrowseFilterSuggestionsQuery
        {
            Field = "Studio",
            SearchText = "Warner",
            Limit = 20
        };

        var results = await handler.Handle(query, CancellationToken.None);

        results.Should().Contain("Warner Bros.");
    }

    [Test]
    public async Task Handle_EmptySearch_ShouldReturnEmpty()
    {
        var handler = new GetMediaBrowseFilterSuggestionsQueryHandler(_context, new TestUser());
        var query = new GetMediaBrowseFilterSuggestionsQuery
        {
            Field = nameof(SmartPlaylistField.ActorName),
            SearchText = "",
            Limit = 20
        };

        var results = await handler.Handle(query, CancellationToken.None);

        results.Should().BeEmpty();
    }

    private void SeedData()
    {
        var now = DateTimeOffset.UtcNow;
        var person = new Person { Id = Guid.NewGuid(), Name = "Leonardo DiCaprio", Created = now, LastModified = now };
        var movie = new Movie
        {
            Id = Guid.NewGuid(),
            Title = "Inception",
            Studios = ["Warner Bros."],
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

        _context.Persons.Add(person);
        _context.Medias.Add(movie);
        _context.SaveChanges();
    }

    private sealed class TestUser : K7.Server.Application.Common.Interfaces.IUser
    {
        public string? IdentityId => null;
        public Guid? Id => Guid.NewGuid();
    }
}
