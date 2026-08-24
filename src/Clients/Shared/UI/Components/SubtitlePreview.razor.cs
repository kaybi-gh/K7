using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class SubtitlePreview
{
    [Inject] private IDeviceService DeviceService { get; set; } = default!;

    [Parameter] public SubtitleFontFamily Family { get; set; }
    [Parameter] public SubtitleFontSize Size { get; set; }
    [Parameter] public string FontColor { get; set; } = "#FFFFFF";
    [Parameter] public double BackgroundOpacity { get; set; } = 0.5;
    [Parameter] public bool ShadowEnabled { get; set; } = true;
    [Parameter] public string ShadowColor { get; set; } = "#000000";
    [Parameter] public double ShadowBlur { get; set; } = 3;

    private DeviceType _deviceType = DeviceType.Desktop;

    protected override async Task OnInitializedAsync()
    {
        _deviceType = DeviceService.CachedDeviceType ?? await DeviceService.GetDeviceTypeAsync();
    }

    private string FontFamilyCss => SubtitleStyleHelper.ToFontFamilyCss(Family);

    private string FontSizePx => SubtitleStyleHelper.ToFontSizeCss(Size, _deviceType);

    private string FontColorCss => SubtitleStyleHelper.NormalizeColor(FontColor);

    private string BackgroundCss => SubtitleStyleHelper.ToBackgroundCss(BackgroundOpacity);

    private string TextShadowStyle
    {
        get
        {
            var shadow = SubtitleStyleHelper.ToTextShadowCss(ShadowEnabled, ShadowColor, ShadowBlur);
            return shadow == "none" ? "" : $"text-shadow: {shadow};";
        }
    }
}
