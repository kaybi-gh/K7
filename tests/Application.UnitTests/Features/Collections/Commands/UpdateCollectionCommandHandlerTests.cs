using Ardalis.GuardClauses;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Collections.Commands.UpdateCollection;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Collections.Commands;

[TestFixture]
public class UpdateCollectionCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private UpdateCollectionCommandHandler _handler = null!;
    private Guid _userId;
    private Guid _collectionId;
    private Guid _groupId;

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
        _collectionId = Guid.NewGuid();
        _groupId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = _groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Collections.Add(new Collection
        {
            Id = _collectionId,
            Title = "Favs",
            UserId = _userId,
            VisibilityScope = VisibilityScope.Federation,
            IsPublic = true
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new UpdateCollectionCommandHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldKeepFederationVisibility_WhenVisibilityScopeIsSent()
    {
        await _handler.Handle(new UpdateCollectionCommand
        {
            Id = _collectionId,
            Title = "Favs",
            VisibilityScope = VisibilityScope.Federation
        }, CancellationToken.None);

        var collection = await _context.Collections.SingleAsync(c => c.Id == _collectionId);
        collection.VisibilityScope.Should().Be(VisibilityScope.Federation);
        collection.IsPublic.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldDowngradeFederation_WhenOnlyIsPublicIsSent()
    {
        await _handler.Handle(new UpdateCollectionCommand
        {
            Id = _collectionId,
            Title = "Favs",
            IsPublic = true
        }, CancellationToken.None);

        var collection = await _context.Collections.SingleAsync(c => c.Id == _collectionId);
        collection.VisibilityScope.Should().Be(VisibilityScope.LocalServer);
    }

    [Test]
    public async Task Handle_ShouldPersistLibraryGroupId_WhenDynamicRulesUpdated()
    {
        await _handler.Handle(new UpdateCollectionCommand
        {
            Id = _collectionId,
            Title = "Favs",
            MediaType = MediaType.Movie,
            LibraryGroupId = _groupId,
            RuleFilter = new RuleGroupDto { MatchCondition = RuleMatchCondition.All, Items = [] }
        }, CancellationToken.None);

        var collection = await _context.Collections.SingleAsync(c => c.Id == _collectionId);
        collection.LibraryGroupId.Should().Be(_groupId);
        collection.RuleFilter.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_ShouldClearLibraryGroupId_WhenOmittedOnDynamicUpdate()
    {
        var collection = await _context.Collections.SingleAsync(c => c.Id == _collectionId);
        collection.MediaType = MediaType.Movie;
        collection.LibraryGroupId = _groupId;
        collection.RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All, Items = [] };
        await _context.SaveChangesAsync();

        await _handler.Handle(new UpdateCollectionCommand
        {
            Id = _collectionId,
            Title = "Favs",
            MediaType = MediaType.Movie,
            RuleFilter = new RuleGroupDto { MatchCondition = RuleMatchCondition.All, Items = [] }
        }, CancellationToken.None);

        collection = await _context.Collections.SingleAsync(c => c.Id == _collectionId);
        collection.LibraryGroupId.Should().BeNull();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenNotOwner()
    {
        _currentUser.Id.Returns(Guid.NewGuid());

        var act = () => _handler.Handle(new UpdateCollectionCommand
        {
            Id = _collectionId,
            Title = "Favs"
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
