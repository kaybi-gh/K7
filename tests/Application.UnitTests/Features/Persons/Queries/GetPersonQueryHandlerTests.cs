using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Persons.Queries.GetPerson;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Metadatas.PersonRoles;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Persons.Queries;

[TestFixture]
public class GetPersonQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private GetPersonQueryHandler _handler = null!;

    private Guid _userId;
    private Guid _personId;
    private Guid _allowedMediaId;
    private Guid _restrictedMediaId;

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

        _userId = Guid.NewGuid();
        _personId = Guid.NewGuid();
        _allowedMediaId = Guid.NewGuid();
        _restrictedMediaId = Guid.NewGuid();
        var libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = groupId,
            MediaType = LibraryMediaType.Movie,
            Title = "Movies",
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });

        var allowed = new Movie
        {
            Id = _allowedMediaId,
            Title = "Allowed",
            IndexedFiles = [CreateIndexedFile(libraryId, "allowed.mkv", 1)]
        };
        var restricted = new Movie
        {
            Id = _restrictedMediaId,
            Title = "Restricted",
            IndexedFiles = [CreateIndexedFile(libraryId, "restricted.mkv", 2)]
        };
        _context.Medias.AddRange(allowed, restricted);

        var person = new Person { Id = _personId, Name = "Actor" };
        person.Roles.Add(new Actor
        {
            PersonId = _personId,
            MediaId = _allowedMediaId,
            Media = allowed,
            CharacterName = "Hero"
        });
        person.Roles.Add(new Actor
        {
            PersonId = _personId,
            MediaId = _restrictedMediaId,
            Media = restricted,
            CharacterName = "Villain"
        });
        _context.Persons.Add(person);
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _handler = new GetPersonQueryHandler(_context, _currentUser, new MediaAccessFilter(_context));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldKeepRestrictedRoles_WhenNoRestrictionProfile()
    {
        var result = await _handler.Handle(new GetPersonQuery(_personId), CancellationToken.None);

        result.Roles.Select(r => r.MediaId).Should().BeEquivalentTo([_allowedMediaId, _restrictedMediaId]);
    }

    [Test]
    public async Task Handle_ShouldHideRestrictedRoles_WhenPersonalRestrictionProfileIsAssigned()
    {
        var profile = new ContentRestrictionProfile
        {
            Name = "Kids",
            RuleFilter = new RuleGroup
            {
                MatchCondition = RuleMatchCondition.Any,
                Items =
                [
                    new ConditionRuleItem
                    {
                        Field = nameof(DynamicPlaylistField.Title),
                        Operator = RuleOperator.Equals,
                        Value = "Restricted"
                    }
                ]
            }
        };
        _context.ContentRestrictionProfiles.Add(profile);
        profile.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(new GetPersonQuery(_personId), CancellationToken.None);

        result.Roles.Should().ContainSingle();
        result.Roles[0].MediaId.Should().Be(_allowedMediaId);
    }

    private static IndexedFile CreateIndexedFile(Guid libraryId, string name, uint hash) => new()
    {
        LibraryId = libraryId,
        Name = name,
        Extension = ".mkv",
        Path = $@"C:\media\{name}",
        Hash = hash,
        Size = 10
    };
}
