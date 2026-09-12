using K7.Server.Domain.Common;
using K7.Server.Domain.Events;
using K7.Server.Infrastructure.Database.Context.Data.Interceptors;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace K7.Server.Application.UnitTests.Infrastructure;

[TestFixture]
public class DispatchDomainEventsInterceptorTests
{
    [Test]
    public async Task SaveChangesAsync_ShouldPublishEvents_FromDeletedEntities()
    {
        var mediator = Substitute.For<IMediator>();
        var published = new List<INotification>();
        mediator.Publish(Arg.Any<INotification>(), Arg.Any<CancellationToken>())
            .Returns(ci =>
            {
                published.Add(ci.ArgAt<INotification>(0));
                return Task.CompletedTask;
            });

        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var interceptor = new DispatchDomainEventsInterceptor(mediator);
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        await using (var setup = new TestDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Items.Add(new TestItem { Id = Guid.NewGuid(), Name = "a" });
            await setup.SaveChangesAsync();
        }

        published.Clear();

        await using (var db = new TestDbContext(options))
        {
            var item = await db.Items.SingleAsync();
            item.AddDomainEvent(new UserDeletedEvent(item.Id, "a", null, "User"));
            db.Items.Remove(item);
            await db.SaveChangesAsync();
        }

        published.Should().ContainSingle()
            .Which.Should().BeOfType<UserDeletedEvent>();
    }

    private sealed class TestItem : BaseEntity
    {
        public string Name { get; set; } = "";
    }

    private sealed class TestDbContext(DbContextOptions<TestDbContext> options) : DbContext(options)
    {
        public DbSet<TestItem> Items => Set<TestItem>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<TestItem>(e =>
            {
                e.HasKey(x => x.Id);
                e.Ignore(x => x.DomainEvents);
            });
        }
    }
}
