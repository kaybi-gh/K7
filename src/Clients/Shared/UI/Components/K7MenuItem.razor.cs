using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7MenuItem
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public bool Disabled { get; set; }

    /// <summary>Main action of the menu (play, open): shown first and emphasized.</summary>
    [Parameter] public bool Primary { get; set; }

    /// <summary>Destructive or hiding action: shown in the error color, in the last section.</summary>
    [Parameter] public bool Danger { get; set; }

    /// <summary>Short secondary text on the right, e.g. a position or a count.</summary>
    [Parameter] public string? Hint { get; set; }

    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public bool PreventClose { get; set; }

    [CascadingParameter] private K7Menu? ParentMenu { get; set; }

    private async Task OnItemClick()
    {
        if (!PreventClose)
            ParentMenu?.Close();
        await OnClick.InvokeAsync();
    }
}
