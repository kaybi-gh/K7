using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Home.Queries.GetEffectiveHomeLayout;
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
public class GetEffectiveHomeLayoutQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUserSettingsService _userSettings = null!;
    private ISharedProfileSettingsService _sharedProfileSettings = null!;
    private IServerSettingsService _serverSettings = null!;
    private IHomeLayoutMaintenanceService _homeLayoutMaintenance = null!;
    private IHomeRecommendationService _homeRecommendation = null!;
    private IUser _currentUser = null!;
    private GetEffectiveHomeLayoutQueryHandler _handler = null!;
    private Guid _userId;

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
        _userSettings = Substitute.For<IUserSettingsService>();
        _sharedProfileSettings = Substitute.For<ISharedProfileSettingsService>();
        _serverSettings = Substitute.For<IServerSettingsService>();
        _homeLayoutMaintenance = Substitute.For<IHomeLayoutMaintenanceService>();
        _homeRecommendation = Substitute.For<IHomeRecommendationService>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _handler = new GetEffectiveHomeLayoutQueryHandler(
            _userSettings,
            _sharedProfileSettings,
            _serverSettings,
            _context,
            _homeLayoutMaintenance,
            _homeRecommendation,
            _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldScopeNewlyAddedRowsByLibraryGroup_WhenNoStoredLayout()
    {
        var groupId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

        _context.LibraryGroups.Add(new LibraryGroup
        {
            Id = groupId,
            Title = "Movies",
            MediaType = LibraryMediaType.Movie
        });
        _context.Libraries.Add(new Library
        {
            Id = Guid.NewGuid(),
            LibraryGroupId = groupId,
            Title = "Movies 01",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en"
        });
        await _context.SaveChangesAsync();

        _userSettings.GetAsync(_userId, UserSettingKeys.HomeLayout, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _serverSettings.GetAsync(ServerSettingKeys.HomeLayout, Arg.Any<CancellationToken>())
            .Returns((string?)null);
        _homeRecommendation.GetBecauseYouWatchedTitleAsync(_userId, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        var result = await _handler.Handle(new GetEffectiveHomeLayoutQuery(), CancellationToken.None);

        var newlyAdded = result.Rows.Should()
            .ContainSingle(r => r.Title.StartsWith(HomeLayoutRowTitles.NewlyAddedInPrefix, StringComparison.Ordinal))
            .Subject;

        newlyAdded.Id.Should().Be(groupId);
        newlyAdded.LibraryGroupIds.Should().Equal(groupId);
        newlyAdded.LibraryIds.Should().BeNull();
        newlyAdded.OrderBy.Should().Equal(MediaOrderingOption.CreatedDesc);
    }
}
