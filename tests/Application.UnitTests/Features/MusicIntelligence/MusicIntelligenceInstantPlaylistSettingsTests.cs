using System.Text.Json;
using FluentAssertions;
using K7.Shared.Dtos;

namespace K7.Server.Application.UnitTests.Features.MusicIntelligence;

[TestFixture]
public class MusicIntelligenceInstantPlaylistSettingsTests
{
    [Test]
    public void SettingsDto_MissingInstantPlaylistEnabled_DefaultsToFalse()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var dto = JsonSerializer.Deserialize<MusicIntelligenceSettingsDto>(
            """{"enabled":true,"baseUrl":"http://localhost:8000","apiKey":"x"}""",
            options);

        dto.Should().NotBeNull();
        dto!.Enabled.Should().BeTrue();
        dto.InstantPlaylistEnabled.Should().BeFalse();
    }

    [Test]
    public void SettingsDto_InstantPlaylistEnabledTrue_RoundTrips()
    {
        var json = JsonSerializer.Serialize(new MusicIntelligenceSettingsDto
        {
            Enabled = true,
            BaseUrl = "http://localhost:8000",
            InstantPlaylistEnabled = true
        });

        var dto = JsonSerializer.Deserialize<MusicIntelligenceSettingsDto>(json);

        dto!.InstantPlaylistEnabled.Should().BeTrue();
    }

    [Test]
    public void StatusDto_InstantPlaylistAvailable_IsIndependentFlag()
    {
        var status = new MusicIntelligenceStatusDto
        {
            IsEnabled = true,
            IsAvailable = false,
            InstantPlaylistEnabled = true,
            InstantPlaylistAvailable = true
        };

        // Visibility is settings-gated; reachability stays on IsAvailable.
        status.InstantPlaylistAvailable.Should().BeTrue();
        status.IsAvailable.Should().BeFalse();
    }
}
