using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.Appearance.Commands.DeleteAppearancePreferences;
using K7.Server.Application.Features.Appearance.Commands.UpdateAppearancePreferences;
using K7.Server.Domain.Settings;
using K7.Shared.Theming;

namespace K7.Server.Application.UnitTests.Features.Appearance;

[TestFixture]
public class UpdateAppearancePreferencesCommandHandlerTests
{
    private IUserSettingsService _userSettings = null!;
    private IUser _currentUser = null!;
    private IUserAppearanceNotifier _notifier = null!;
    private Guid _userId;

    [SetUp]
    public void SetUp()
    {
        _userId = Guid.NewGuid();
        _userSettings = Substitute.For<IUserSettingsService>();
        _currentUser = Substitute.For<IUser>();
        _currentUser.Id.Returns(_userId);
        _currentUser.IdentityId.Returns("identity-user");
        _notifier = Substitute.For<IUserAppearanceNotifier>();
    }

    [Test]
    public async Task Handle_ShouldPersistAndNotifyHub_WhenIdentityIsPresent()
    {
        var preferences = new AppearancePreferencesDto { Default = { ThemeId = "oled" } };
        var handler = new UpdateAppearancePreferencesCommandHandler(_userSettings, _currentUser, _notifier);

        await handler.Handle(new UpdateAppearancePreferencesCommand { Preferences = preferences }, CancellationToken.None);

        await _userSettings.Received(1).SetAsync(_userId, UserSettingKeys.Appearance, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyAppearancePreferencesUpdatedAsync("identity-user", preferences, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task Handle_ShouldPersistWithoutHubNotify_WhenIdentityIsMissing()
    {
        _currentUser.IdentityId.Returns((string?)null);
        var handler = new UpdateAppearancePreferencesCommandHandler(_userSettings, _currentUser, _notifier);

        await handler.Handle(new UpdateAppearancePreferencesCommand { Preferences = new AppearancePreferencesDto() }, CancellationToken.None);

        await _userSettings.Received(1).SetAsync(_userId, UserSettingKeys.Appearance, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().NotifyAppearancePreferencesUpdatedAsync(default!, default!, default);
    }

    [Test]
    public async Task Delete_ShouldSendTheServerLookToTheOtherDevices()
    {
        var handler = new DeleteAppearancePreferencesCommandHandler(_userSettings, _currentUser, _notifier);

        await handler.Handle(new DeleteAppearancePreferencesCommand(), CancellationToken.None);

        await _userSettings.Received(1).RemoveAsync(_userId, UserSettingKeys.Appearance, Arg.Any<CancellationToken>());
        await _notifier.Received(1).NotifyAppearancePreferencesUpdatedAsync(
            "identity-user",
            Arg.Is<AppearancePreferencesDto>(p => p.Default.IsDefault && p.DeviceOverrides.Count == 0),
            Arg.Any<CancellationToken>());
    }
}
