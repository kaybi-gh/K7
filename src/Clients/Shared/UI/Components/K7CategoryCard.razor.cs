using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7CategoryCard
{
    /// <summary>Primary title displayed in large bold italic caps.</summary>
    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    /// <summary>Secondary line displayed below the title in small uppercase.</summary>
    [Parameter] public string Description { get; set; } = string.Empty;

    /// <summary>Phosphor icon name (without the "ph-" prefix).</summary>
    [Parameter] public string Icon { get; set; } = string.Empty;

    /// <summary>CSS color for the left edge of the background gradient.</summary>
    [Parameter] public string GradientStart { get; set; } = "rgba(80,20,20,0.85)";

    /// <summary>CSS color for the right edge of the background gradient.</summary>
    [Parameter] public string GradientEnd { get; set; } = "rgba(20,20,20,0.85)";

    /// <summary>Background color of the icon badge.</summary>
    [Parameter] public string IconColor { get; set; } = "rgba(0,0,0,0.55)";

    /// <summary>Optional background image URL. Displayed behind the gradient overlay.</summary>
    [Parameter] public string? ImageUrl { get; set; }

    /// <summary>Optional hex color extracted from the cover image. Overrides gradient colors when provided.</summary>
    [Parameter] public string? DominantColor { get; set; }

    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string ComputedGradientStart => !string.IsNullOrEmpty(DominantColor)
        ? $"{DominantColor}e0"
        : GradientStart;

    private string ComputedGradientEnd => !string.IsNullOrEmpty(DominantColor)
        ? $"{DominantColor}40"
        : GradientEnd;

    private string ComputedColor => !string.IsNullOrEmpty(DominantColor)
        ? DominantColor
        : GradientStart;
}
