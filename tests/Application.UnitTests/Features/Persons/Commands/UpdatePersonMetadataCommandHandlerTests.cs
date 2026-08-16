using K7.Server.Application.Features.Persons.Commands.UpdatePersonMetadata;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Persons.Commands;

[TestFixture]
public class UpdatePersonMetadataCommandHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private UpdatePersonMetadataCommandHandler _handler = null!;
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

        _personId = Guid.NewGuid();
        _context.Persons.Add(new Person
        {
            Id = _personId,
            Name = "Actor",
            ExternalIds =
            [
                new ExternalId { ProviderName = "imdb", Value = "nm1111111" }
            ]
        });
        _context.SaveChanges();

        _handler = new UpdatePersonMetadataCommandHandler(_context);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldReplaceImdbExternalId_WhenPersonAlreadyHasOne()
    {
        await _handler.Handle(new UpdatePersonMetadataCommand
        {
            Id = _personId,
            LockedFields = [],
            ExternalIds =
            [
                new ExternalIdEditDto { ProviderName = "imdb", Value = "nm9999999" }
            ]
        }, CancellationToken.None);

        _context.ChangeTracker.Clear();
        var saved = await _context.ExternalIds
            .Where(e => e.PersonId == _personId)
            .ToListAsync();

        saved.Should().ContainSingle(e => e.ProviderName == "imdb" && e.Value == "nm9999999");
        saved.Should().NotContain(e => e.Value == "nm1111111");
    }
}
