using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class TrailerPlaybackHelperTests
{
    [Test]
    public void Pick_ShouldReturnTrailerType_WhenPresent()
    {
        var teaser = Create("teaser", "Teaser");
        var trailer = Create("abc", "Trailer");

        TrailerPlaybackHelper.Pick([teaser, trailer]).Should().BeSameAs(trailer);
    }

    [Test]
    public void Pick_ShouldReturnFirst_WhenNoTrailerType()
    {
        var first = Create("one", "Teaser");
        var second = Create("two", "Clip");

        TrailerPlaybackHelper.Pick([first, second]).Should().BeSameAs(first);
    }

    [Test]
    public void Pick_ShouldReturnNull_WhenEmpty()
    {
        TrailerPlaybackHelper.Pick(null).Should().BeNull();
        TrailerPlaybackHelper.Pick([]).Should().BeNull();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeFalse_WhenWebDesktopYouTube()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Web, DeviceType.Desktop, "YouTube", false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeFalse_WhenWebPhoneYouTube()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Web, DeviceType.Phone, "YouTube", false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeFalse_WhenNativeAndSettingOff()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Native, DeviceType.Desktop, "YouTube", false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeTrue_WhenNativeAndSettingOn()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Native, DeviceType.Desktop, "YouTube", true)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeFalse_WhenTvAndSettingOff()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Web, DeviceType.TV, "YouTube", false)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeTrue_WhenTvAndSettingOn()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Web, DeviceType.TV, "YouTube", true)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeFalse_WhenWebDesktopAndSettingOn()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Web, DeviceType.Desktop, "YouTube", true)
            .Should().BeFalse();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeTrue_WhenSiteNotEmbeddable()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Web, DeviceType.Desktop, "External", false)
            .Should().BeTrue();
    }

    [Test]
    public void ShouldOpenExternally_ShouldBeFalse_WhenWebDesktopAndSiteMissing()
    {
        TrailerPlaybackHelper.ShouldOpenExternally(ClientType.Web, DeviceType.Desktop, null, false)
            .Should().BeFalse();
    }

    [Test]
    public void TryBuildWatchUrl_ShouldReturnYouTubeWatchUrl()
    {
        TrailerPlaybackHelper.TryBuildWatchUrl("YouTube", "dQw4w9wgGcQ")
            .Should().Be("https://www.youtube.com/watch?v=dQw4w9wgGcQ");
    }

    [Test]
    public void TryBuildWatchUrl_ShouldReturnNull_WhenSiteNotEmbeddableAndKeyIsNotUrl()
    {
        TrailerPlaybackHelper.TryBuildWatchUrl("External", "abc")
            .Should().BeNull();
    }

    [Test]
    public void TryBuildWatchUrl_ShouldReturnAbsoluteHttpUrl_WhenKeyIsUrl()
    {
        TrailerPlaybackHelper.TryBuildWatchUrl("External", "https://example.com/trailer")
            .Should().Be("https://example.com/trailer");
    }

    [Test]
    public void TryBuildEmbedUrl_ShouldReturnYouTubeEmbedWithoutNocookie()
    {
        var url = TrailerPlaybackHelper.TryBuildEmbedUrl("YouTube", "dQw4w9wgGcQ");

        url.Should().StartWith("https://www.youtube.com/embed/dQw4w9wgGcQ?");
        url.Should().Contain("autoplay=1");
        url.Should().NotContain("nocookie");
    }

    [Test]
    public void TryBuildEmbedUrl_ShouldReturnNull_WhenSiteNotEmbeddable()
    {
        TrailerPlaybackHelper.TryBuildEmbedUrl("External", "https://example.com/trailer")
            .Should().BeNull();
    }

    private static TrailerDto Create(string key, string type) => new()
    {
        Key = key,
        Name = type,
        Site = "YouTube",
        Type = type
    };
}
