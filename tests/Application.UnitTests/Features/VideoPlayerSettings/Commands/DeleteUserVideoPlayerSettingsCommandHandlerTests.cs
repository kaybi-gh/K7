using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.VideoPlayerSettings.Commands.DeleteUserVideoPlayerSettings;
using K7.Server.Application.Features.VideoPlayerSettings.Queries.GetEffectiveVideoPlayerSettings;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos;
using K7.Shared.Enums;
using MediatR;

namespace K7.Server.Application.UnitTests.Features.VideoPlayerSettings.Commands;

[TestFixture]
public class DeleteUserVideoPlayerSettingsCommandHandlerTests
{
    private IUserSettingsService _userSettings = null!;
    private IUser _currentUser = null!;
    private ISender _sender = null!;
    private IUserVideoPlayerSettingsNotifier _notifier = null!;
    private DeleteUserVideoPlayerSettingsCommandHandler _handler = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _userId = Guid.NewGuid();
        _userSettings = Substitute.For<IUserSettingsService>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.IdentityId.Returns("identity-user");
        _sender = Substitute.For<ISender>();
        _notifier = Substitute.For<IUserVideoPlayerSettingsNotifier>();
        _handler = new DeleteUserVideoPlayerSettingsCommandHandler(_userSettings, _currentUser, _sender, _notifier);
    }

    [Test]
    public async Task Handle_ShouldRemovePersistedSettingsAndNotifyEffectiveDefaults()
    {
        var effective = new VideoPlayerSettingsDto { SubtitleFontSize = SubtitleFontSize.Small };
        _sender.Send(Arg.Any<GetEffectiveVideoPlayerSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(effective);

        await _handler.Handle(new DeleteUserVideoPlayerSettingsCommand(), CancellationToken.None);

        await _userSettings.Received(1).RemoveAsync(
            _userId,
            UserSettingKeys.VideoPlayerSettings,
            Arg.Any<CancellationToken>());
        await _sender.Received(1).Send(
            Arg.Any<GetEffectiveVideoPlayerSettingsQuery>(),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyVideoPlayerSettingsUpdatedAsync(
            "identity-user",
            effective,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldRemoveWithoutHubNotify_WhenIdentityIsMissing()
    {
        _currentUser.IdentityId.Returns((string?)null);
        _sender.Send(Arg.Any<GetEffectiveVideoPlayerSettingsQuery>(), Arg.Any<CancellationToken>())
            .Returns(new VideoPlayerSettingsDto());

        await _handler.Handle(new DeleteUserVideoPlayerSettingsCommand(), CancellationToken.None);

        await _userSettings.Received(1).RemoveAsync(
            _userId,
            UserSettingKeys.VideoPlayerSettings,
            Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyVideoPlayerSettingsUpdatedAsync(
            default!, default!, default);
    }
}
