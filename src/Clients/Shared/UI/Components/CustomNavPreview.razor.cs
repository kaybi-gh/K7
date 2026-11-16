using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// A small drawing of the app with the custom navigation applied, shown next to its settings:
/// the shortcuts as a bar under the top bar, or as a row of tiles among the rows of the page.
/// A sketch made of blocks, like <see cref="SkinPreview"/>: nothing is loaded.
/// </summary>
public partial class CustomNavPreview
{
    /// <summary>Shortcuts the sketch has room for.</summary>
    public const int MaxShortcuts = 5;

    private const int CardCount = 8;

    /// <summary>Custom navigation on or off. Off draws the plain page.</summary>
    [Parameter] public bool Enabled { get; set; }

    [Parameter] public CustomNavPlacement Placement { get; set; }

    /// <summary>Bar placement: an icon before each shortcut.</summary>
    [Parameter] public bool ShowIcons { get; set; }

    /// <summary>Row placement: the title of the row. Empty draws the row without a title.</summary>
    [Parameter] public string? RowTitle { get; set; }

    /// <summary>Titles of the shortcuts, in order. Empty draws a few blank ones.</summary>
    [Parameter] public IReadOnlyList<string> Items { get; set; } = [];

    /// <summary>What the drawing is, for assistive technologies.</summary>
    [Parameter] public string? AriaLabel { get; set; }

    private bool ShowsBar => Enabled && Placement == CustomNavPlacement.Bar;

    private bool ShowsRow => Enabled && Placement == CustomNavPlacement.FeedRow;

    private IEnumerable<string> Shortcuts => Items.Count > 0
        ? Items.Take(MaxShortcuts)
        : Enumerable.Repeat("", 3);
}
