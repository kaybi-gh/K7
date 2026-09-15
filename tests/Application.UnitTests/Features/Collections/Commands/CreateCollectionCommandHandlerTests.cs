using Ardalis.GuardClauses;
using FluentValidation;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Collections.Commands.CreateCollection;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Rules;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Collections.Commands;

[TestFixture]
public class CreateCollectionCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private CreateCollectionCommandHandler _handler = null!;
    private Guid _userId;
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
        _groupId = Guid.NewGuid();
        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = _groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new CreateCollectionCommandHandler(_context, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldPersistLibraryGroupId_WhenDynamic()
    {
        var id = await _handler.Handle(new CreateCollectionCommand
        {
            Title = "Scoped",
            MediaType = MediaType.Movie,
            LibraryGroupId = _groupId,
            RuleFilter = new RuleGroupDto { MatchCondition = RuleMatchCondition.All, Items = [] }
        }, CancellationToken.None);

        var collection = await _context.Collections.SingleAsync(c => c.Id == id);
        collection.LibraryGroupId.Should().Be(_groupId);
        collection.RuleFilter.Should().NotBeNull();
    }

    [Test]
    public async Task Handle_ShouldThrow_WhenLibraryGroupIdWithoutRules()
    {
        var act = () => _handler.Handle(new CreateCollectionCommand
        {
            Title = "Manual",
            LibraryGroupId = _groupId
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task Handle_ShouldThrowNotFound_WhenLibraryGroupMissing()
    {
        var act = () => _handler.Handle(new CreateCollectionCommand
        {
            Title = "Scoped",
            MediaType = MediaType.Movie,
            LibraryGroupId = Guid.NewGuid(),
            RuleFilter = new RuleGroupDto { MatchCondition = RuleMatchCondition.All, Items = [] }
        }, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }
}
