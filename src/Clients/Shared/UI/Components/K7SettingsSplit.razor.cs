using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Settings page split into an index and one sub-page at a time. Wide screens show both side by
/// side. Narrow screens and TV behave like a menu: the index alone, then the sub-page as its own
/// screen (page header hidden, back link on top) once <see cref="DetailOpen"/> is set, so no
/// screen needs a long vertical scroll.
/// </summary>
public partial class K7SettingsSplit
{
    /// <summary>Page header. Hidden while a sub-page is open on narrow screens and TV.</summary>
    [Parameter] public RenderFragment? Header { get; set; }

    /// <summary>Index entries, usually <see cref="K7SettingsNavItem"/>.</summary>
    [Parameter, EditorRequired] public RenderFragment? Index { get; set; }

    /// <summary>Content of the current sub-page.</summary>
    [Parameter, EditorRequired] public RenderFragment? Detail { get; set; }

    /// <summary>Actions under the index that apply to the whole page, e.g. reset to defaults.</summary>
    [Parameter] public RenderFragment? Footer { get; set; }

    /// <summary>Name of the index, used for the back link and as the navigation label.</summary>
    [Parameter, EditorRequired] public string IndexLabel { get; set; } = "";

    [Parameter, EditorRequired] public string DetailTitle { get; set; } = "";

    /// <summary>Address of the index alone, target of the back link.</summary>
    [Parameter, EditorRequired] public string BackHref { get; set; } = "";

    /// <summary>
    /// True when the user picked a sub-page. False shows the index alone on narrow screens and
    /// TV, while wide screens still show <see cref="Detail"/> as the default sub-page.
    /// </summary>
    [Parameter] public bool DetailOpen { get; set; }

    /// <summary>Save indicator shown beside <see cref="Footer"/> on pages that save on change.</summary>
    [Parameter] public SettingsSaveState SaveState { get; set; }

    /// <summary>
    /// Gives the index more room on wide screens, for lists of items whose lines carry a
    /// secondary text and chips (<see cref="K7EntityRow"/>) rather than short section names.
    /// </summary>
    [Parameter] public bool WideIndex { get; set; }

    [Parameter] public string Class { get; set; } = "";
}
