using System.Globalization;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class SubtitlePreview
{
    [Parameter] public SubtitleFontFamily Family { get; set; }
    [Parameter] public SubtitleFontSize Size { get; set; }
    [Parameter] public string FontColor { get; set; } = "#FFFFFF";
    [Parameter] public double BackgroundOpacity { get; set; } = 0.5;
    [Parameter] public bool ShadowEnabled { get; set; } = true;
    [Parameter] public string ShadowColor { get; set; } = "#000000";
    [Parameter] public double ShadowBlur { get; set; } = 3;

    private string FontFamilyCss => Family switch
    {
        SubtitleFontFamily.Manrope => "'Manrope', sans-serif",
        SubtitleFontFamily.Epilogue => "'Epilogue', sans-serif",
        SubtitleFontFamily.SansSerif => "sans-serif",
        SubtitleFontFamily.Serif => "serif",
        SubtitleFontFamily.Monospace => "monospace",
        _ => "inherit"
    };

    private string FontSizePx => Size switch
    {
        SubtitleFontSize.Small => "14px",
        SubtitleFontSize.Large => "22px",
        _ => "18px"
    };

    private string TextShadowStyle => ShadowEnabled
        ? $"text-shadow: 0 0 {ShadowBlur.ToString(CultureInfo.InvariantCulture)}px {ShadowColor}, 1px 1px {ShadowBlur.ToString(CultureInfo.InvariantCulture)}px {ShadowColor};"
        : "";
}
