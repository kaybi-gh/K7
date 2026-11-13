using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Entry of a settings index: icon, title and a short description,
/// linking to the sub-page that edits them. Used as the index of <see cref="K7SettingsSplit"/>.
/// </summary>
public partial class K7SettingsNavItem
{
    [Parameter, EditorRequired] public string Href { get; set; } = "";
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter] public string? Icon { get; set; }

    /// <summary>One short line saying what the sub-page is for.</summary>
    [Parameter] public string? Description { get; set; }
    [Parameter] public bool Active { get; set; }
    [Parameter] public string Class { get; set; } = "";
}
