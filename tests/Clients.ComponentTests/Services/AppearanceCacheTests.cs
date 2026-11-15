using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared;
using K7.Shared.Enums;
using K7.Shared.Theming;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class AppearanceCacheTests
{
    // A storage that keeps what it is given, like the device does.
    private static IDeviceStorageService Storage(string? initial = null)
    {
        var stored = initial;
        var storage = Substitute.For<IDeviceStorageService>();
        storage.When(s => s.Set(PreferenceKeys.APPEARANCE_PREFERENCES_CACHE, Arg.Any<string>()))
            .Do(call => stored = call.ArgAt<string>(1));
        storage.Get(PreferenceKeys.APPEARANCE_PREFERENCES_CACHE, Arg.Any<string?>()).Returns(_ => stored);
        return storage;
    }

    private static AppearancePreferencesDto Preferences() => new()
    {
        Default = { ThemeId = "midnight", TextSize = AppearanceTextSize.Large },
        DeviceOverrides = { [DeviceType.Phone] = new AppearanceSettingsDto { ThemeId = "paper" } },
        Cards = new MediaCardPreferencesDto
        {
            Default = { Details = [CardDetailField.Year, CardDetailField.Rating], Hover = CardHoverStyle.Zoom },
            Overrides = { [CardMediaGroup.Music] = new MediaCardStyleDto { ShowSubtitle = false } }
        }
    };

    [Test]
    public void TryLoad_ShouldReturnWhatSaveStored_ForTheSameUser()
    {
        var storage = Storage();
        var serverCards = new MediaCardPreferencesDto { Default = { Hover = CardHoverStyle.Lift } };

        AppearanceCache.Save(storage, "user-a", Preferences(), serverCards);
        var cached = AppearanceCache.TryLoad(storage, "user-a");

        cached.Should().NotBeNull();
        cached!.Preferences.Should().BeEquivalentTo(Preferences());
        cached.ServerDefaultCards.Should().BeEquivalentTo(serverCards);
    }

    [Test]
    public void TryLoad_ShouldKeepAUserWithoutCardStyleFollowingTheServer()
    {
        var storage = Storage();

        AppearanceCache.Save(storage, "user-a", new AppearancePreferencesDto(), null);
        var cached = AppearanceCache.TryLoad(storage, "user-a");

        cached!.Preferences.Cards.Should().BeNull();
        cached.ServerDefaultCards.Should().BeNull();
    }

    [Test]
    public void TryLoad_ShouldReturnNothing_ForAnotherUserOrNoUser()
    {
        var storage = Storage();
        AppearanceCache.Save(storage, "user-a", Preferences(), null);

        AppearanceCache.TryLoad(storage, "user-b").Should().BeNull();
        AppearanceCache.TryLoad(storage, null).Should().BeNull();
        AppearanceCache.TryLoad(storage, "").Should().BeNull();
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not json")]
    [TestCase("{\"UserId\":\"user-a\"}")]
    public void TryLoad_ShouldReturnNothing_WhenTheStoredValueIsMissingOrUnreadable(string? stored)
    {
        AppearanceCache.TryLoad(Storage(stored), "user-a").Should().BeNull();
    }

    [Test]
    public void Save_ShouldStoreNothing_WhenThereIsNoUser()
    {
        var storage = Storage();

        AppearanceCache.Save(storage, null, Preferences(), null);

        storage.DidNotReceive().Set(PreferenceKeys.APPEARANCE_PREFERENCES_CACHE, Arg.Any<string>());
    }
}
