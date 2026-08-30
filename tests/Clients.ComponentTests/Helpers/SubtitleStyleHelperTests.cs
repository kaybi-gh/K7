using K7.Clients.Shared.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Enums;

namespace K7.Clients.ComponentTests.Helpers;

[TestFixture]
public class SubtitleStyleHelperTests
{
    [Test]
    public void ToCss_ShouldMapDesktopDefaults_WhenDtoUsesDefaults()
    {
        var css = SubtitleStyleHelper.ToCss(new VideoPlayerSettingsDto(), DeviceType.Desktop);

        css.FontFamily.Should().Be("inherit");
        css.FontSize.Should().Be("22px");
        css.Color.Should().Be("#FFFFFF");
        css.BackgroundColor.Should().Be("rgba(0, 0, 0, 0.5)");
        css.TextShadow.Should().Contain("#000000");
    }

    [Test]
    public void ToCss_ShouldScaleLargeForTv()
    {
        var settings = new VideoPlayerSettingsDto { SubtitleFontSize = SubtitleFontSize.Large };

        SubtitleStyleHelper.ToCss(settings, DeviceType.TV).FontSize.Should().Be("60px");
        SubtitleStyleHelper.ToCss(settings, DeviceType.Desktop).FontSize.Should().Be("32px");
        SubtitleStyleHelper.ToCss(settings, DeviceType.Phone).FontSize.Should().Be("20px");
    }

    [Test]
    public void ToCss_ShouldScaleMediumForTv()
    {
        var settings = new VideoPlayerSettingsDto { SubtitleFontSize = SubtitleFontSize.Medium };

        SubtitleStyleHelper.ToCss(settings, DeviceType.TV).FontSize.Should().Be("40px");
    }

    [Test]
    public void ToCss_ShouldDisableShadow_WhenShadowDisabled()
    {
        var settings = new VideoPlayerSettingsDto
        {
            SubtitleShadowEnabled = false,
            SubtitleFontFamily = SubtitleFontFamily.Manrope,
            SubtitleFontSize = SubtitleFontSize.Large,
            SubtitleFontColor = "#FFCC00",
            SubtitleBackgroundOpacity = 0.25
        };

        var css = SubtitleStyleHelper.ToCss(settings, DeviceType.Desktop);

        css.FontFamily.Should().Be("'Manrope', sans-serif");
        css.FontSize.Should().Be("32px");
        css.Color.Should().Be("#FFCC00");
        css.BackgroundColor.Should().Be("rgba(0, 0, 0, 0.25)");
        css.TextShadow.Should().Be("none");
    }

    [Test]
    public void TryParseHexColor_ShouldParseShortAndLongHex()
    {
        SubtitleStyleHelper.TryParseHexColor("#ABC", out var a1, out var r1, out var g1, out var b1)
            .Should().BeTrue();
        a1.Should().Be(255);
        r1.Should().Be(0xAA);
        g1.Should().Be(0xBB);
        b1.Should().Be(0xCC);

        SubtitleStyleHelper.TryParseHexColor("#80FF0000", out var a2, out var r2, out var g2, out var b2)
            .Should().BeTrue();
        a2.Should().Be(0x80);
        r2.Should().Be(0xFF);
        g2.Should().Be(0);
        b2.Should().Be(0);
    }

    [Test]
    public void ToFontSizePx_ShouldMapWatchLikePhone()
    {
        SubtitleStyleHelper.ToFontSizePx(SubtitleFontSize.Medium, DeviceType.Watch).Should().Be(16);
        SubtitleStyleHelper.ToFontSizePx(SubtitleFontSize.Medium, DeviceType.Tablet).Should().Be(18);
    }
}
