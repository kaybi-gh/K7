using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class PersonCard
{
    /// <summary>Where the card leads. Null shows the card without a link.</summary>
    [Parameter] public string? Href { get; set; }

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    [Parameter] public string? Subtitle { get; set; }

    [Parameter] public string? ImageUrl { get; set; }

    [Parameter] public string Alt { get; set; } = "";

    /// <summary>The card style preferences in effect: a person card follows their common style.</summary>
    [CascadingParameter] public MediaCardStyleContext? CardStyles { get; set; }

    // Same hover and focus effect as the media cards around it. The outline is the default.
    private string? HoverClass => MediaCardStyleContext.Resolve(CardStyles, null).Hover switch
    {
        CardHoverStyle.Zoom => "personrole-item--hover-zoom",
        CardHoverStyle.Lift => "personrole-item--hover-lift",
        _ => null
    };
}
