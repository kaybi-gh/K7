using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Application.Features.Notifications.Services.Descriptors;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.UnitTests.Features.Notifications.Services;

[TestFixture]
public class NotificationParamsTests
{
    [Test]
    public void Globals_ShouldIncludeServerAndUnixTimeSamples()
    {
        NotificationParams.Globals.Should().Contain(p => p.Name == "Server.Name" && p.SampleValue.Length > 0);
        NotificationParams.Globals.Should().Contain(p => p.Name == "Current.UnixTime");
    }

    [Test]
    public void PlaybackStateChanged_ShouldIncludeTvMusicAndExternalIds()
    {
        NotificationParams.PlaybackStateChanged.Should().Contain(p => p.Name == "Show.Name");
        NotificationParams.PlaybackStateChanged.Should().Contain(p => p.Name == "Artist.Name");
        NotificationParams.PlaybackStateChanged.Should().Contain(p => p.Name == "External.Tmdb");
        NotificationParams.PlaybackStateChanged.Should().Contain(p => p.Group == NotificationParameterGroup.Session);
    }

    [Test]
    public void AllDescriptors_ShouldExposeSampleValues()
    {
        var descriptors = new INotificationEventDescriptor[]
        {
            new PlaybackStateChangedEventDescriptor(),
            new MediaPlaybackCompletedEventDescriptor(),
            new MediaCreatedEventDescriptor()
        };

        foreach (var descriptor in descriptors)
        {
            descriptor.Parameters.Should().OnlyContain(p => !string.IsNullOrWhiteSpace(p.SampleValue));
            descriptor.DisplayNameKey.Should().StartWith("Event");
        }
    }

    [Test]
    public void WebhookPresets_ShouldIncludeDiscordAndTelegram()
    {
        NotificationWebhookPresets.All.Should().Contain(p => p.Id == "discord");
        NotificationWebhookPresets.All.Should().Contain(p => p.Id == "telegram");
        NotificationWebhookPresets.All.Should().OnlyContain(p => p.RawJsonTemplate.Contains("{{", StringComparison.Ordinal));
    }
}
