using System.Text.Json;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Json;

[TestFixture]
public class ServerFeatureFlagsDtoSerializationTests
{
    [Test]
    public void Deserialize_ShouldEnableMediaLinkPreviews_WhenStoredJsonOmitsProperty()
    {
        var flags = JsonSerializer.Deserialize<ServerFeatureFlagsDto>(
            """{"FederationEnabled":true,"FederationInvitationsEnabled":false}""");

        flags.Should().NotBeNull();
        flags!.MediaLinkPreviewsEnabled.Should().BeTrue();
        flags.FederationEnabled.Should().BeTrue();
        flags.FederationInvitationsEnabled.Should().BeFalse();
    }

    [Test]
    public void Deserialize_ShouldKeepMediaLinkPreviewsOff_WhenStoredFalse()
    {
        var flags = JsonSerializer.Deserialize<ServerFeatureFlagsDto>(
            """{"FederationEnabled":false,"FederationInvitationsEnabled":true,"MediaLinkPreviewsEnabled":false}""");

        flags.Should().NotBeNull();
        flags!.MediaLinkPreviewsEnabled.Should().BeFalse();
    }

    [Test]
    public void WithFederationFields_ShouldPreserveMediaLinkPreviews_WhenTheyWereOptedOut()
    {
        var current = new ServerFeatureFlagsDto
        {
            FederationEnabled = false,
            FederationInvitationsEnabled = true,
            MediaLinkPreviewsEnabled = false
        };

        var saved = current with
        {
            FederationEnabled = true,
            FederationInvitationsEnabled = false
        };

        saved.MediaLinkPreviewsEnabled.Should().BeFalse();
        saved.FederationEnabled.Should().BeTrue();
        saved.FederationInvitationsEnabled.Should().BeFalse();
    }

    [Test]
    public void WithMediaLinkPreviews_ShouldPreserveFederationFields()
    {
        var current = new ServerFeatureFlagsDto
        {
            FederationEnabled = true,
            FederationInvitationsEnabled = false,
            MediaLinkPreviewsEnabled = true
        };

        var saved = current with { MediaLinkPreviewsEnabled = false };

        saved.MediaLinkPreviewsEnabled.Should().BeFalse();
        saved.FederationEnabled.Should().BeTrue();
        saved.FederationInvitationsEnabled.Should().BeFalse();
    }

    [Test]
    public void NewDto_ShouldEnableMediaLinkPreviews_ByDefault()
    {
        new ServerFeatureFlagsDto().MediaLinkPreviewsEnabled.Should().BeTrue();
    }
}
