using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Paper
{
    [Parameter] public RenderFragment? ChildContent { get; set; }

    /// <summary>Elevation level 0-4. 0 = flat (no shadow). Higher = stronger shadow.</summary>
    [Parameter] public int Elevation { get; set; } = 1;

    /// <summary>Renders a visible border instead of (or in addition to) shadow.</summary>
    [Parameter] public bool Outlined { get; set; }

    /// <summary>Disables border-radius to render square corners.</summary>
    [Parameter] public bool Square { get; set; }

    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter(CaptureUnmatchedValues = true)] public Dictionary<string, object>? AdditionalAttributes { get; set; }
}
