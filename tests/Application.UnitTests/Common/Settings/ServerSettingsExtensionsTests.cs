using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Settings;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Common.Settings;

[TestFixture]
public class ServerSettingsExtensionsTests
{
    [Test]
    public async Task GetFeatureFlagsAsync_ShouldEnableMediaLinkPreviews_WhenUnset()
    {
        var settings = Substitute.For<IServerSettingsService>();
        settings.GetAsync(ApplicationSettingKeys.FeatureFlags, Arg.Any<CancellationToken>())
            .Returns((ServerFeatureFlagsDto?)null);

        var flags = await settings.GetFeatureFlagsAsync();

        flags.MediaLinkPreviewsEnabled.Should().BeTrue();
    }

    [Test]
    public async Task GetFeatureFlagsAsync_ShouldKeepMediaLinkPreviewsOff_WhenStoredFalse()
    {
        var settings = Substitute.For<IServerSettingsService>();
        settings.GetAsync(ApplicationSettingKeys.FeatureFlags, Arg.Any<CancellationToken>())
            .Returns(new ServerFeatureFlagsDto { MediaLinkPreviewsEnabled = false });

        var flags = await settings.GetFeatureFlagsAsync();

        flags.MediaLinkPreviewsEnabled.Should().BeFalse();
        flags.FederationInvitationsEnabled.Should().BeTrue();
    }
}
