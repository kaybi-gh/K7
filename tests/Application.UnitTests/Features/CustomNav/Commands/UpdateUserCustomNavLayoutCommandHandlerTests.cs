using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.CustomNav.Commands.UpdateUserCustomNavLayout;
using K7.Server.Application.Features.CustomNav.Queries.GetEffectiveCustomNavLayout;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;
using MediatR;

namespace K7.Server.Application.UnitTests.Features.CustomNav.Commands;

[TestFixture]
public class UpdateUserCustomNavLayoutCommandHandlerTests
{
    private IUserSettingsService _userSettings = null!;
    private IUser _currentUser = null!;
    private ISender _sender = null!;
    private IUserCustomNavNotifier _notifier = null!;
    private UpdateUserCustomNavLayoutCommandHandler _handler = null!;
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
        _handler = new UpdateUserCustomNavLayoutCommandHandler(
            _userSettings,
            _currentUser,
            _sender,
            _notifier);
    }

    [Test]
    public async Task Handle_ShouldPersistAndNotifyHub_WhenIdentityIsPresent()
    {
        var layout = CustomNavLayoutDto.Disabled() with
        {
            Enabled = true,
            Placement = CustomNavPlacement.Bar,
            BarScope = CustomNavBarScope.Everywhere
        };
        var effective = layout with { FeedRowTitle = "effective" };
        _sender.Send(Arg.Any<GetEffectiveCustomNavLayoutQuery>(), Arg.Any<CancellationToken>())
            .Returns(effective);

        await _handler.Handle(new UpdateUserCustomNavLayoutCommand { Layout = layout }, CancellationToken.None);

        await _userSettings.Received(1).SetAsync(
            _userId,
            UserSettingKeys.CustomNavLayout,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyCustomNavLayoutUpdatedAsync(
            "identity-user",
            effective,
            Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldPersistWithoutHubNotify_WhenIdentityIsMissing()
    {
        _currentUser.IdentityId.Returns((string?)null);
        var layout = CustomNavLayoutDto.Disabled();

        await _handler.Handle(new UpdateUserCustomNavLayoutCommand { Layout = layout }, CancellationToken.None);

        await _userSettings.Received(1).SetAsync(
            _userId,
            UserSettingKeys.CustomNavLayout,
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyCustomNavLayoutUpdatedAsync(
            default!, default!, default);
        await _sender.DidNotReceiveWithAnyArgs().Send(default!, default);
    }
}
