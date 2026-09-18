using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.CustomNav.Commands.DeleteUserCustomNavLayout;
using K7.Server.Application.Features.CustomNav.Queries.GetEffectiveCustomNavLayout;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.CustomNav;
using MediatR;

namespace K7.Server.Application.UnitTests.Features.CustomNav.Commands;

[TestFixture]
public class DeleteUserCustomNavLayoutCommandHandlerTests
{
    private IUserSettingsService _userSettings = null!;
    private IUser _currentUser = null!;
    private ISender _sender = null!;
    private IUserCustomNavNotifier _notifier = null!;
    private DeleteUserCustomNavLayoutCommandHandler _handler = null!;
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
        _notifier = Substitute.For<IUserCustomNavNotifier>();
        _handler = new DeleteUserCustomNavLayoutCommandHandler(
            _userSettings,
            _currentUser,
            _sender,
            _notifier);
    }

    [Test]
    public async Task Handle_ShouldRemoveAndNotifyEffectiveLayout_WhenIdentityIsPresent()
    {
        var effective = CustomNavLayoutDto.Disabled();
        _sender.Send(Arg.Any<GetEffectiveCustomNavLayoutQuery>(), Arg.Any<CancellationToken>())
            .Returns(effective);

        await _handler.Handle(new DeleteUserCustomNavLayoutCommand(), CancellationToken.None);

        await _userSettings.Received(1).RemoveAsync(
            _userId,
            UserSettingKeys.CustomNavLayout,
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyCustomNavLayoutUpdatedAsync(
            "identity-user",
            effective,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldRemoveWithoutHubNotify_WhenIdentityIsMissing()
    {
        _currentUser.IdentityId.Returns((string?)null);

        await _handler.Handle(new DeleteUserCustomNavLayoutCommand(), CancellationToken.None);

        await _userSettings.Received(1).RemoveAsync(
            _userId,
            UserSettingKeys.CustomNavLayout,
            Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyCustomNavLayoutUpdatedAsync(
            default!, default!, default);
        await _sender.DidNotReceiveWithAnyArgs().Send(default!, default);
    }
}
