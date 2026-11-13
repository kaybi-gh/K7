using K7.Clients.Shared.UI.Helpers;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SettingsMirrorTests
{
    [TestCase("/settings/interface", "/admin/interface")]
    [TestCase("/settings/interface/theme-songs", "/admin/interface/theme-songs")]
    [TestCase("/settings/audio-player/equalizer", "/admin/audio-playback/equalizer")]
    [TestCase("/settings/video-playback/subtitles", "/admin/video-playback/subtitles")]
    [TestCase("/settings/libraries", "/admin/libraries")]
    [TestCase("/settings/scrobbling", "/admin/scrobbling")]
    public void GetCounterpart_ShouldLeadToTheAdminSubPage_WhenOnPersonalSettings(string path, string expected)
    {
        var result = SettingsMirror.GetCounterpart(path, out var toAdmin);

        result.Should().Be(expected);
        toAdmin.Should().BeTrue();
    }

    [TestCase("/admin/interface/home", "/settings/interface/home")]
    [TestCase("/admin/audio-playback", "/settings/audio-player")]
    [TestCase("/admin/video-playback/tracks", "/settings/video-playback/tracks")]
    public void GetCounterpart_ShouldLeadToThePersonalSubPage_WhenOnAdmin(string path, string expected)
    {
        var result = SettingsMirror.GetCounterpart(path, out var toAdmin);

        result.Should().Be(expected);
        toAdmin.Should().BeFalse();
    }

    [TestCase("/settings/account")]
    [TestCase("/admin/users")]
    [TestCase("/settings/interfaces")]
    [TestCase("/")]
    public void GetCounterpart_ShouldReturnNull_WhenThePageHasNoMirror(string path)
    {
        SettingsMirror.GetCounterpart(path, out _).Should().BeNull();
    }
}
