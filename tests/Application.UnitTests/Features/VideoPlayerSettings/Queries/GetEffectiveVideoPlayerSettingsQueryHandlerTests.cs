using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.VideoPlayerSettings.Queries.GetEffectiveVideoPlayerSettings;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Features.VideoPlayerSettings.Queries;

[TestFixture]
public class GetEffectiveVideoPlayerSettingsQueryHandlerTests
{
    private IUserSettingsService _userSettings = null!;
    private IServerSettingsService _serverSettings = null!;
    private IUser _currentUser = null!;
    private GetEffectiveVideoPlayerSettingsQueryHandler _handler = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _userId = Guid.NewGuid();
        _userSettings = Substitute.For<IUserSettingsService>();
        _serverSettings = Substitute.For<IServerSettingsService>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _handler = new GetEffectiveVideoPlayerSettingsQueryHandler(_userSettings, _serverSettings, _currentUser);
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
}
