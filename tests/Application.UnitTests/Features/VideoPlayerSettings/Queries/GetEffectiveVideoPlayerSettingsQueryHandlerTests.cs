using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.VideoPlayerSettings.Queries.GetEffectiveVideoPlayerSettings;
using K7.Server.Domain.Entities.Users;
using K7.Server.Domain.Settings;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Shared.Dtos;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.UnitTests.Features.VideoPlayerSettings.Queries;

[TestFixture]
public class GetEffectiveVideoPlayerSettingsQueryHandlerTests
{
    private SqliteConnection _connection = null!;
    private ApplicationDbContext _context = null!;
    private IUserSettingsService _userSettings = null!;
    private IServerSettingsService _serverSettings = null!;
    private IUser _currentUser = null!;
    private GetEffectiveVideoPlayerSettingsQueryHandler _handler = null!;
    private Guid _userId;
    private Guid _hostId;
    private Guid _sharedProfileId;

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
        _hostId = Guid.NewGuid();
        _sharedProfileId = Guid.NewGuid();

        _context.Users.AddRange(
            new User { Id = _userId, DisplayName = "member" },
            new User { Id = _hostId, DisplayName = "host" });
        _context.SharedProfiles.Add(new SharedProfile
        {
            Id = _sharedProfileId,
            Name = "Couple",
            HostUserId = _hostId,
            CreatedByUserId = _hostId,
            Members =
            [
                new SharedProfileMember { Id = Guid.NewGuid(), SharedProfileId = _sharedProfileId, UserId = _hostId },
                new SharedProfileMember { Id = Guid.NewGuid(), SharedProfileId = _sharedProfileId, UserId = _userId }
            ]
        });
        _context.SaveChanges();

        _userSettings = Substitute.For<IUserSettingsService>();
        _serverSettings = Substitute.For<IServerSettingsService>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns((Guid?)null);

        _handler = new GetEffectiveVideoPlayerSettingsQueryHandler(
            _context, _userSettings, _serverSettings, _currentUser);
    }

    [TearDown]
    public void TearDown()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Test]
    public async Task Handle_ShouldDefaultRememberTrackSelectionToTrue_WhenNull()
    {
        _userSettings.GetAsync(_userId, UserSettingKeys.VideoPlayerSettings, Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new VideoPlayerSettingsDto { RememberTrackSelection = null }));

        var result = await _handler.Handle(new GetEffectiveVideoPlayerSettingsQuery(), CancellationToken.None);

        result.RememberTrackSelection.Should().BeTrue();
    }

    [Test]
    public async Task Handle_ShouldPreserveRememberTrackSelectionFalse()
    {
        _userSettings.GetAsync(_userId, UserSettingKeys.VideoPlayerSettings, Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new VideoPlayerSettingsDto { RememberTrackSelection = false }));

        var result = await _handler.Handle(new GetEffectiveVideoPlayerSettingsQuery(), CancellationToken.None);

        result.RememberTrackSelection.Should().BeFalse();
    }

    [Test]
    public async Task Handle_ShouldUseHostRememberTrackSelection_WhenSharedProfileActive()
    {
        _currentUser.GetSharedProfileIdAsync(Arg.Any<CancellationToken>()).Returns(_sharedProfileId);
        _userSettings.GetAsync(_userId, UserSettingKeys.VideoPlayerSettings, Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new VideoPlayerSettingsDto { RememberTrackSelection = true }));
        _userSettings.GetAsync(_hostId, UserSettingKeys.VideoPlayerSettings, Arg.Any<CancellationToken>())
            .Returns(JsonSerializer.Serialize(new VideoPlayerSettingsDto { RememberTrackSelection = false }));

        var result = await _handler.Handle(new GetEffectiveVideoPlayerSettingsQuery(), CancellationToken.None);

        result.RememberTrackSelection.Should().BeFalse();
    }
}
