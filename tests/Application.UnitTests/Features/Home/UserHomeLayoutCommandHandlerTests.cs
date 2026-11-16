using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Home.Commands.DeleteUserHomeLayout;
using K7.Server.Application.Features.Home.Commands.UpdateUserHomeLayout;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.UnitTests.Features.Home;

[TestFixture]
public class UserHomeLayoutCommandHandlerTests
{
    private IUserSettingsService _userSettings = null!;
    private IUser _currentUser = null!;
    private IUserHomeLayoutNotifier _notifier = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _userId = Guid.NewGuid();
        _userSettings = Substitute.For<IUserSettingsService>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.IdentityId.Returns("identity-user");
        _notifier = Substitute.For<IUserHomeLayoutNotifier>();
    }

    [Test]
    public async Task Update_ShouldPersistAndTellTheOtherDevices()
    {
        var handler = new UpdateUserHomeLayoutCommandHandler(_userSettings, _currentUser, _notifier);

        await handler.Handle(new UpdateUserHomeLayoutCommand { Layout = new HomeLayoutDto { Rows = [] } }, CancellationToken.None);

        await _userSettings.Received(1).SetAsync(_userId, UserSettingKeys.HomeLayout, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyHomeLayoutUpdatedAsync("identity-user", Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Update_ShouldPersistWithoutHubNotify_WhenIdentityIsMissing()
    {
        _currentUser.IdentityId.Returns((string?)null);
        var handler = new UpdateUserHomeLayoutCommandHandler(_userSettings, _currentUser, _notifier);

        await handler.Handle(new UpdateUserHomeLayoutCommand { Layout = new HomeLayoutDto { Rows = [] } }, CancellationToken.None);

        await _userSettings.Received(1).SetAsync(_userId, UserSettingKeys.HomeLayout, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyHomeLayoutUpdatedAsync(default!, default);
    }

    [Test]
    public async Task Delete_ShouldTellTheOtherDevices()
    {
        var handler = new DeleteUserHomeLayoutCommandHandler(_userSettings, _currentUser, _notifier);

        await handler.Handle(new DeleteUserHomeLayoutCommand(), CancellationToken.None);

        await _userSettings.Received(1).RemoveAsync(_userId, UserSettingKeys.HomeLayout, Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyHomeLayoutUpdatedAsync("identity-user", Arg.Any<CancellationToken>());
    }
}
