using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// One setting on one line: label and hint on the left, a compact control on the right.
/// Falls back to a stacked layout on narrow screens, or always when <see cref="Stacked"/> is set
/// (sliders, text fields, option lists).
/// </summary>
public partial class K7SettingRow
{
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter] public string? Hint { get; set; }
    /// <summary>Extra hint line with markup, e.g. a <see cref="K7HintLink"/> or a second note.</summary>
    [Parameter] public RenderFragment? HintContent { get; set; }

    [Parameter] public bool Stacked { get; set; }

    /// <summary>Marks the label like a required field's own label, which the row replaces.</summary>
    [Parameter] public bool Required { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }
}
