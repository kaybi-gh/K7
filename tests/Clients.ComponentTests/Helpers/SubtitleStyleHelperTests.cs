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

    [TestCase(SubtitleFontSize.Small, DeviceType.Phone, 20)]
    [TestCase(SubtitleFontSize.Medium, DeviceType.Phone, 28)]
    [TestCase(SubtitleFontSize.Large, DeviceType.Phone, 36)]
    [TestCase(SubtitleFontSize.Small, DeviceType.Tablet, 22)]
    [TestCase(SubtitleFontSize.Medium, DeviceType.Tablet, 32)]
    [TestCase(SubtitleFontSize.Large, DeviceType.Tablet, 42)]
    [TestCase(SubtitleFontSize.Small, DeviceType.TV, 24)]
    [TestCase(SubtitleFontSize.Medium, DeviceType.TV, 36)]
    [TestCase(SubtitleFontSize.Large, DeviceType.TV, 52)]
    [TestCase(SubtitleFontSize.Small, DeviceType.Desktop, 16)]
    [TestCase(SubtitleFontSize.Medium, DeviceType.Desktop, 22)]
    [TestCase(SubtitleFontSize.Large, DeviceType.Desktop, 32)]
    [TestCase(SubtitleFontSize.Medium, DeviceType.Watch, 28)]
    public void ToFontSizePx_ShouldScaleByDevice(SubtitleFontSize size, DeviceType device, int expectedPx)
    {
        SubtitleStyleHelper.ToFontSizePx(size, device).Should().Be(expectedPx);
        SubtitleStyleHelper.ToFontSizeSp(size, device).Should().Be(expectedPx);
        SubtitleStyleHelper.ToFontSizeCss(size, device).Should().Be($"{expectedPx}px");
    }

    [Test]
    public void ToFontSizePx_ShouldKeepPhoneCuesAtLeastBodyText()
    {
        SubtitleStyleHelper.ToFontSizePx(SubtitleFontSize.Small, DeviceType.Phone)
            .Should().BeGreaterThanOrEqualTo(20);
        SubtitleStyleHelper.ToFontSizePx(SubtitleFontSize.Medium, DeviceType.Phone)
            .Should().BeGreaterThanOrEqualTo(24);
        SubtitleStyleHelper.ToFontSizePx(SubtitleFontSize.Large, DeviceType.Phone)
            .Should().BeGreaterThanOrEqualTo(32);
    }

    [Test]
    public void ToCss_ShouldScaleLargeForTv()
    {
        var settings = new VideoPlayerSettingsDto { SubtitleFontSize = SubtitleFontSize.Large };

        SubtitleStyleHelper.ToCss(settings, DeviceType.TV).FontSize.Should().Be("52px");
        SubtitleStyleHelper.ToCss(settings, DeviceType.Desktop).FontSize.Should().Be("32px");
        SubtitleStyleHelper.ToCss(settings, DeviceType.Phone).FontSize.Should().Be("36px");
    }

    [Test]
    public void ToCss_ShouldScaleMediumForTv()
    {
        var settings = new VideoPlayerSettingsDto { SubtitleFontSize = SubtitleFontSize.Medium };

        SubtitleStyleHelper.ToCss(settings, DeviceType.TV).FontSize.Should().Be("36px");
        SubtitleStyleHelper.ToCss(settings, DeviceType.Phone).FontSize.Should().Be("28px");
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
}
