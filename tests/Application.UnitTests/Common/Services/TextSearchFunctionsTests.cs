using K7.Server.Application.Common.QueryExtensions;
using K7.Server.Application.Common.Services;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Server.Infrastructure.Database.Context.Data.Interceptors;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Common.Services;

[TestFixture]
public class TextSearchFunctionsTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;

    [SetUp]
    public void SetUp()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        SqliteFoldDiacriticsInterceptor.Register(_connection);

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
    public async Task WhereNameMatches_ShouldMatchPerson_WhenQueryOmitsDiacritics()
    {
        _context.Persons.Add(new Person { Id = Guid.NewGuid(), Name = "Amélie" });
        _context.Persons.Add(new Person { Id = Guid.NewGuid(), Name = "Inception" });
        await _context.SaveChangesAsync();

        var names = await _context.Persons
            .WhereNameMatches(
                MediaTextSearchHelper.BuildTitlePattern("amelie", supportsTrigramSearch: true),
                MediaTextSearchHelper.BuildSortTitlePattern("amelie", supportsTrigramSearch: true))
            .Select(person => person.Name)
            .ToListAsync();

        names.Should().Equal("Amélie");
    }

    [Test]
    public async Task WhereCharacterNameMatches_ShouldMatchRole_WhenQueryOmitsDiacritics()
    {
        var person = new Person { Id = Guid.NewGuid(), Name = "Bruce Willis" };
        var movie = new Movie { Id = Guid.NewGuid(), Title = "Film", SortTitle = "Film" };
        person.Roles.Add(new Actor
        {
            Person = person,
            Media = movie,
            CharacterName = "Élément"
        });
        person.Roles.Add(new VoiceActor
        {
            Person = person,
            Media = movie,
            CharacterName = "Korben"
        });
        _context.Persons.Add(person);
        await _context.SaveChangesAsync();

        var actorNames = await _context.PersonRoles.OfType<Actor>()
            .WhereCharacterNameMatches(
                MediaTextSearchHelper.BuildTitlePattern("element", supportsTrigramSearch: true),
                MediaTextSearchHelper.BuildSortTitlePattern("element", supportsTrigramSearch: true))
            .Select(role => role.CharacterName)
            .ToListAsync();

        var voiceNames = await _context.PersonRoles.OfType<VoiceActor>()
            .WhereCharacterNameMatches(
                MediaTextSearchHelper.BuildTitlePattern("element", supportsTrigramSearch: true),
                MediaTextSearchHelper.BuildSortTitlePattern("element", supportsTrigramSearch: true))
            .Select(role => role.CharacterName)
            .ToListAsync();

        actorNames.Should().Equal("Élément");
        voiceNames.Should().BeEmpty();
    }
}
