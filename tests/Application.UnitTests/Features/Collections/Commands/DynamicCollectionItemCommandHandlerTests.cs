using FluentValidation;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Collections.Commands.AddCollectionItem;
using K7.Server.Application.Features.Collections.Commands.RemoveCollectionItem;
using K7.Server.Domain.Entities.Collections;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Models;
using K7.Server.Infrastructure.Database.Context.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Collections.Commands;

[TestFixture]
public class DynamicCollectionItemCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUser _currentUser = null!;
    private Guid _userId;
    private Guid _collectionId;

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
        _context.Users.Add(new User { Id = _userId, DisplayName = "owner" });
        _context.Collections.Add(new Collection
        {
            Id = _collectionId,
            Title = "Dynamic",
            UserId = _userId,
            MediaType = MediaType.Movie,
            RuleFilter = new RuleGroup { MatchCondition = RuleMatchCondition.All, Items = [] }
        });
        _context.SaveChanges();

        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task AddItem_ShouldThrow_WhenCollectionIsDynamic()
    {
        var handler = new AddCollectionItemCommandHandler(_context, _currentUser);

        var act = () => handler.Handle(new AddCollectionItemCommand
        {
            CollectionId = _collectionId,
            MediaId = Guid.NewGuid()
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task RemoveItem_ShouldThrow_WhenCollectionIsDynamic()
    {
        var handler = new RemoveCollectionItemCommandHandler(_context, _currentUser);

        var act = () => handler.Handle(new RemoveCollectionItemCommand
        {
            CollectionId = _collectionId,
            ItemId = Guid.NewGuid()
        }, CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
