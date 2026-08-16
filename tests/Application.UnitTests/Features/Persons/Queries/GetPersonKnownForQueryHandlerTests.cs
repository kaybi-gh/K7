using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Services;
using K7.Server.Application.Features.Persons.Queries.GetPersonKnownFor;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Entities.Restrictions;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Persons.Queries;

[TestFixture]
public class GetPersonKnownForQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private IPersonCreditsProvider _creditsProvider = null!;
    private GetPersonKnownForQueryHandler _handler = null!;

    private Guid _userId;
    private Guid _personId;

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

        _context.Users.Add(new User { Id = _userId, DisplayName = "viewer" });
        _context.Persons.Add(new Person
        {
            Id = _personId,
            Name = "Actor",
            ExternalIds = [new ExternalId { ProviderName = "tmdb", Value = "99" }]
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _creditsProvider = Substitute.For<IPersonCreditsProvider>();
        _creditsProvider.GetPersonCreditsAsync("99", Arg.Any<CancellationToken>())
            .Returns([
                new ExternalPersonCredit
                {
                    ExternalId = "1",
                    Title = "Sensitive Title",
                    Year = 2020,
                    MediaType = "movie",
                    PosterPath = "https://image.tmdb.org/sensitive.jpg",
                    Popularity = 10
                }
            ]);

        _handler = new GetPersonKnownForQueryHandler(
            _context,
            _creditsProvider,
            _currentUser,
            new MediaAccessFilter(_context));
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReturnCredits_WhenNoRestrictionProfile()
    {
        var result = await _handler.Handle(
            new GetPersonKnownForQuery { PersonId = _personId },
            CancellationToken.None);

        result.Should().ContainSingle(c => c.Title == "Sensitive Title");
    }

    [Test]
    public async Task Handle_ShouldReturnEmpty_WhenPersonalRestrictionProfileIsAssigned()
    {
        var profile = CreateRestrictionProfile("Kids");
        _context.ContentRestrictionProfiles.Add(profile);
        profile.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));
        await _context.SaveChangesAsync();

        var result = await _handler.Handle(
            new GetPersonKnownForQuery { PersonId = _personId },
            CancellationToken.None);

        result.Should().BeEmpty();
        await _creditsProvider.DidNotReceive().GetPersonCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnEmpty_WhenSharedProfileHasRestrictionAssigned()
    {
        var sharedProfileId = Guid.NewGuid();
        var profile = CreateRestrictionProfile("Family");
        _context.ContentRestrictionProfiles.Add(profile);
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Kids",
            HostUserId = _userId,
            CreatedByUserId = _userId,
            ContentRestrictionProfile = profile
        });
        await _context.SaveChangesAsync();
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(sharedProfileId);

        var result = await _handler.Handle(
            new GetPersonKnownForQuery { PersonId = _personId },
            CancellationToken.None);

        result.Should().BeEmpty();
        await _creditsProvider.DidNotReceive().GetPersonCreditsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldReturnCredits_WhenSharedProfileHasNoRestrictionAssigned()
    {
        var sharedProfileId = Guid.NewGuid();
        var personalProfile = CreateRestrictionProfile("Personal");
        _context.ContentRestrictionProfiles.Add(personalProfile);
        personalProfile.Users.Add(await _context.Users.SingleAsync(u => u.Id == _userId));
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = sharedProfileId,
            Name = "Adults",
            HostUserId = _userId,
            CreatedByUserId = _userId
        });
        await _context.SaveChangesAsync();
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(sharedProfileId);

        var result = await _handler.Handle(
            new GetPersonKnownForQuery { PersonId = _personId },
            CancellationToken.None);

        result.Should().ContainSingle(c => c.Title == "Sensitive Title");
    }

    private static ContentRestrictionProfile CreateRestrictionProfile(string name) =>
        new()
        {
            Name = name,
            RuleFilter = new RuleGroup
            {
                MatchCondition = RuleMatchCondition.Any,
                Items =
                [
                    new ConditionRuleItem
                    {
                        Field = nameof(DynamicPlaylistField.Title),
                        Operator = RuleOperator.Equals,
                        Value = "Blocked"
                    }
                ]
            }
        };
}
