using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.VideoPlayerSettings.Commands.UpdateUserVideoPlayerSettings;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Server.Application.UnitTests.Features.VideoPlayerSettings.Commands;

[TestFixture]
public class UpdateUserVideoPlayerSettingsCommandHandlerTests
{
    private IUserSettingsService _userSettings = null!;
    private IUser _currentUser = null!;
    private IUserVideoPlayerSettingsNotifier _notifier = null!;
    private UpdateUserVideoPlayerSettingsCommandHandler _handler = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _userId = Guid.NewGuid();
        _userSettings = Substitute.For<IUserSettingsService>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.IdentityId.Returns("identity-user");
        _notifier = Substitute.For<IUserVideoPlayerSettingsNotifier>();
        _handler = new UpdateUserVideoPlayerSettingsCommandHandler(_userSettings, _currentUser, _notifier);
    }

    [Test]
    public async Task Handle_ShouldPersistAndNotifyHub_WhenIdentityIsPresent()
    {
        var settings = new VideoPlayerSettingsDto
        {
            SkipBackSeconds = 20,
            SubtitleFontSize = SubtitleFontSize.Large
        };

        await _handler.Handle(new UpdateUserVideoPlayerSettingsCommand { Settings = settings }, CancellationToken.None);

        await _userSettings.Received(1).SetAsync(
            _userId,
            UserSettingKeys.VideoPlayerSettings,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyVideoPlayerSettingsUpdatedAsync(
            "identity-user",
            settings,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldPersistWithoutHubNotify_WhenIdentityIsMissing()
    {
        _currentUser.IdentityId.Returns((string?)null);
        var settings = new VideoPlayerSettingsDto();

        await _handler.Handle(new UpdateUserVideoPlayerSettingsCommand { Settings = settings }, CancellationToken.None);

        await _userSettings.Received(1).SetAsync(
            _userId,
            UserSettingKeys.VideoPlayerSettings,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyVideoPlayerSettingsUpdatedAsync(
            default!, default!, default);
    }
}
