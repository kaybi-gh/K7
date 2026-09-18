using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Shared.Interfaces;
using Microsoft.Extensions.Localization;

namespace K7.Clients.ComponentTests.Services;

[TestFixture]
public class MediaBrowseServiceLocalizationTests
{
    [Test]
    public async Task GetRootItems_ShouldUseLocalizedTitles()
    {
        var sut = CreateService(key => key switch
        {
            "Home" => "Accueil",
            "Library" => "Bibliotheque",
            "Playlists" => "Playlists",
            "Downloads" => "Telechargements",
            _ => key
        });

        var items = await sut.GetRootItemsAsync();

        items.Should().Contain(i => i.Id == "root:home" && i.Title == "Accueil");
        items.Should().Contain(i => i.Id == "root:artists" && i.Title == "Bibliotheque");
        items.Should().Contain(i => i.Id == "root:playlists" && i.Title == "Playlists");
        items.Should().Contain(i => i.Id == "root:downloads" && i.Title == "Telechargements");
    }

    private static MediaBrowseService CreateService(Func<string, string> translate)
    {
        var localizer = Substitute.For<IStringLocalizer<MediaBrowseService>>();
        localizer[Arg.Any<string>()].Returns(ci =>
        {
            var key = ci.Arg<string>();
            return new LocalizedString(key, translate(key));
        });
        localizer[Arg.Any<string>(), Arg.Any<object[]>()].Returns(ci =>
        {
            var key = ci.Arg<string>();
            return new LocalizedString(key, translate(key));
        });

        return new MediaBrowseService(
            Substitute.For<IMediaService>(),
            Substitute.For<IPlaylistService>(),
            Substitute.For<IServerInfoService>(),
            Substitute.For<IK7ServerService>(),
            Substitute.For<IOfflineMediaStore>(),
            Substitute.For<IMusicRadioPlaybackService>(),
            Substitute.For<IServerPreferencesService>(),
            Substitute.For<IAudioPlayerService>(),
            localizer);
    }
}
