using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Icon
{
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public K7IconSize IconSize { get; set; } = K7IconSize.Md;
    [Parameter] public string Color { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string CssClass
    {
        get
        {
            var sizeClass = IconSize.ToCssClass();
            var colorStyle = string.IsNullOrEmpty(Color) ? "" : $"color-{Color}";
            return string.Join(" ", new[] { Icon, "k7-icon", sizeClass, colorStyle, Class }.Where(s => !string.IsNullOrEmpty(s)));
        }
    }
}
