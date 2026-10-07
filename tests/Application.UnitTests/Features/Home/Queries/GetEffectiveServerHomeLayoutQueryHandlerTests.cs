using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Home.Queries.GetEffectiveServerHomeLayout;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos.Requests;
using K7.Shared.Home;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.Home.Queries;

[TestFixture]
public class GetEffectiveServerHomeLayoutQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IServerSettingsService _serverSettings = null!;
    private IHomeLayoutMaintenanceService _homeLayoutMaintenance = null!;
    private GetEffectiveServerHomeLayoutQueryHandler _handler = null!;

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

        _serverSettings = Substitute.For<IServerSettingsService>();
        _homeLayoutMaintenance = Substitute.For<IHomeLayoutMaintenanceService>();
        _handler = new GetEffectiveServerHomeLayoutQueryHandler(
            _serverSettings,
            _context,
            _homeLayoutMaintenance);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldScopeNewlyAddedRowsByLibraryGroup_WhenNoServerOverride()
    {
        var groupId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var libraryA = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var libraryB = Guid.Parse("22222222-2222-2222-2222-222222222222");

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Series",
            MediaType = LibraryMediaType.Serie
        });
        _context.Libraries.AddRange(
            new Library
            {
                Id = libraryA,
                LibraryGroupId = groupId,
                Title = "Series 01",
                MediaType = LibraryMediaType.Serie,
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            },
            new Library
            {
                Id = libraryB,
                LibraryGroupId = groupId,
                Title = "Series 02",
                MediaType = LibraryMediaType.Serie,
                MetadataProviderName = "tmdb",
                MetadataLanguage = "fr",
                MetadataFallbackLanguage = "en"
            });
        await _context.SaveChangesAsync();

        _serverSettings.GetAsync(ServerSettingKeys.HomeLayout, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await _handler.Handle(new GetEffectiveServerHomeLayoutQuery(), CancellationToken.None);

        var newlyAdded = result.Rows.Should()
            .ContainSingle(r => r.Title.StartsWith(HomeLayoutRowTitles.NewlyAddedInPrefix, StringComparison.Ordinal))
            .Subject;

        newlyAdded.Id.Should().Be(groupId);
        newlyAdded.LibraryGroupIds.Should().Equal(groupId);
        newlyAdded.LibraryIds.Should().BeNull();
        newlyAdded.OrderBy.Should().Equal(MediaOrderingOption.CreatedDesc);
        await _homeLayoutMaintenance.DidNotReceive()
            .SanitizeAsync(Arg.Any<K7.Shared.Dtos.Home.HomeLayoutDto>(), Arg.Any<CancellationToken>());
    }
}
