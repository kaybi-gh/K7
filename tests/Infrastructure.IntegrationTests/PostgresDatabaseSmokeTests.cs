using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Tests.Helpers.Databases;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Infrastructure.IntegrationTests;

[TestFixture]
public class PostgresDatabaseSmokeTests
{
    private ITestDatabase _database = null!;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        _database = await TestDatabaseFactory.CreateAsync();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        await _database.DisposeAsync();
    }

    [Test]
    public async Task Context_ShouldPersistAndQueryLibrary()
    {
        var libraryId = Guid.NewGuid();
        var groupId = Guid.NewGuid();

        await using (var context = CreateContext())
        {
            context.LibraryGroups.Add(new LibraryGroup
            {
                Id = groupId,
                Title = "Integration Movies",
                MediaType = LibraryMediaType.Movie
            });
            context.Libraries.Add(new Library
            {
                Id = libraryId,
                LibraryGroupId = groupId,
                Title = "Integration Library",
                MediaType = LibraryMediaType.Movie,
                RootPath = "/media",
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            });
            await context.SaveChangesAsync();
        }

        await _database.ResetAsync();

        await using var verifyContext = CreateContext();
        var count = await verifyContext.Libraries.CountAsync();
        count.Should().Be(0);
    }

    private ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_database.GetConnection())
            .Options;

        return new ApplicationDbContext(options);
    }
}
