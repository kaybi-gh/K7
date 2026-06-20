using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class PersonCard
{
    [Parameter, EditorRequired] public string Href { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = default!;

    [Parameter] public string? Subtitle { get; set; }

    [Parameter] public string? ImageUrl { get; set; }

    [Parameter] public string Alt { get; set; } = "";
}
