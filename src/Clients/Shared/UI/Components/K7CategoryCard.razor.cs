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

    /// <summary>CSS color used as the gradient tone when no dominant color is set.</summary>
    [Parameter] public string GradientStart { get; set; } = "rgba(80,20,20,0.85)";

    /// <summary>Legacy gradient end color. Kept for API compatibility.</summary>
    [Parameter] public string GradientEnd { get; set; } = "rgba(20,20,20,0.85)";

    /// <summary>Background color of the icon badge.</summary>
    [Parameter] public string IconColor { get; set; } = "rgba(0,0,0,0.55)";

    /// <summary>Optional background image URL. Displayed behind the gradient overlay.</summary>
    [Parameter] public string? ImageUrl { get; set; }

    /// <summary>Optional hex color extracted from the cover image. Used as the gradient tone when provided.</summary>
    [Parameter] public string? DominantColor { get; set; }

    /// <summary>Optional configured card color (hex). Used when no dominant color is available.</summary>
    [Parameter] public string? CardColor { get; set; }

    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }

    private string ComputedTone => ResolveOpaqueTone(DominantColor ?? CardColor ?? GradientStart);

    private static string ResolveOpaqueTone(string color)
    {
        var trimmed = color.Trim();

        if (trimmed.StartsWith('#'))
        {
            var hex = trimmed[1..];
            return hex.Length >= 6 ? $"#{hex[..6]}" : trimmed;
        }

        if (trimmed.StartsWith("rgba(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = trimmed[5..^1];
            var parts = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && int.TryParse(parts[0], out var r)
                && int.TryParse(parts[1], out var g)
                && int.TryParse(parts[2], out var b))
                return $"#{r:X2}{g:X2}{b:X2}";
        }

        if (trimmed.StartsWith("rgb(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = trimmed[4..^1];
            var parts = inner.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && int.TryParse(parts[0], out var r)
                && int.TryParse(parts[1], out var g)
                && int.TryParse(parts[2], out var b))
                return $"#{r:X2}{g:X2}{b:X2}";
        }

        return trimmed;
    }
}
