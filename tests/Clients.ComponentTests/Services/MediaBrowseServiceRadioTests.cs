using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Dtos;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class MediaBrowseServiceRadioTests
{
    [Test]
    public async Task GetHomeItems_ShouldUseLocalizedRadioTitles()
    {
        var localizer = Substitute.For<IStringLocalizer<MediaBrowseService>>();
        localizer[Arg.Any<string>()].Returns(ci =>
        {
            var key = ci.Arg<string>();
            var value = key switch
            {
                "PresetDiscovery" => "Decouverte aleatoire",
                "PresetTimeCapsule" => "Time Capsule",
                "PresetRecentlyAdded" => "Nouveautes",
                "Radio" => "Radio",
                _ => key
            };
            return new LocalizedString(key, value);
        });

        var serverPreferences = Substitute.For<IServerPreferencesService>();
        serverPreferences.GetMusicIntelligenceStatusAsync(Arg.Any<CancellationToken>())
            .Returns(new MusicIntelligenceStatusDto { IsAvailable = false });

        var sut = new MediaBrowseService(
            Substitute.For<IMediaService>(),
            Substitute.For<IPlaylistService>(),
            Substitute.For<IServerInfoService>(),
            Substitute.For<IK7ServerService>(),
            Substitute.For<IOfflineMediaStore>(),
            Substitute.For<IMusicRadioPlaybackService>(),
            serverPreferences,
            Substitute.For<IAudioPlayerService>(),
            localizer);

        var items = await sut.GetChildrenAsync("root:home");

        items.Should().Contain(i => i.Id == "radio:Discovery" && i.Title == "Decouverte aleatoire");
        items.Should().Contain(i => i.Id == "radio:TimeCapsule" && i.Title == "Time Capsule");
        items.Should().Contain(i => i.Id == "radio:RecentlyAdded" && i.Title == "Nouveautes");
    }
}
