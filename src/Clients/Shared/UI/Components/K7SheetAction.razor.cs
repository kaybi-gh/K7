using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// One action of an item's sheet: a full-width line that opens a dialog or another page.
/// <see cref="Badges"/> say at a glance what is already set, so the dialog is opened only to
/// change it.
/// </summary>
public partial class K7SheetAction
{
    [Parameter, EditorRequired] public string Label { get; set; } = "";
    [Parameter] public string? Icon { get; set; }
    [Parameter] public IReadOnlyList<string>? Badges { get; set; }

    /// <summary>Destructive action, shown in the error color.</summary>
    [Parameter] public bool Danger { get; set; }

    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public string Class { get; set; } = "";
}
