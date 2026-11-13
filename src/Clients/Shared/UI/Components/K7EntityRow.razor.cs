using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Two-level line of an admin list (user, device, rule...): a leading visual, a title with a
/// secondary line, and state chips on the right. A link to the item's sheet by default, a button
/// when <see cref="OnClick"/> is set (selection mode). Used as the index of
/// <see cref="K7SettingsSplit"/>, with the sheet as its detail.
/// </summary>
public partial class K7EntityRow
{
    [Parameter, EditorRequired] public string Title { get; set; } = "";
    [Parameter] public string? Subtitle { get; set; }

    /// <summary>Address of the item's sheet. Ignored when <see cref="OnClick"/> is set.</summary>
    [Parameter] public string Href { get; set; } = "";

    /// <summary>Makes the line a button, e.g. to toggle its selection.</summary>
    [Parameter] public EventCallback OnClick { get; set; }

    /// <summary>Avatar or icon in front of the text.</summary>
    [Parameter] public RenderFragment? Leading { get; set; }

    /// <summary>State chips on the right.</summary>
    [Parameter] public RenderFragment? Trailing { get; set; }

    /// <summary>The item whose sheet is open.</summary>
    [Parameter] public bool Active { get; set; }

    /// <summary>Null hides the selection mark, otherwise it shows checked or not.</summary>
    [Parameter] public bool? Checked { get; set; }

    /// <summary>Fades the leading visual, for a disabled or inactive item.</summary>
    [Parameter] public bool Dimmed { get; set; }

    /// <summary>Plays a short highlight, for an item another page linked to.</summary>
    [Parameter] public bool Highlighted { get; set; }

    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string? Id { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private string CssClass
    {
        get
        {
            var classes = new List<string>(6) { "k7-entity-row", "focusable" };
            if (Active)
                classes.Add("k7-entity-row--active");
            if (Checked == true)
                classes.Add("k7-entity-row--checked");
            if (Highlighted)
                classes.Add("k7-entity-row--highlighted");
            if (!string.IsNullOrEmpty(Class))
                classes.Add(Class);
            return string.Join(' ', classes);
        }
    }
}
