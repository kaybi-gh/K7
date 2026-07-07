using K7.Clients.Shared.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaPageContent
{
    [Parameter]
    public RenderFragment? ChildContent { get; set; }

    [Parameter]
    public bool Tinted { get; set; }

    [Parameter]
    public bool FullWash { get; set; }

    [Parameter]
    public string? DominantColor { get; set; }

    [Parameter]
    public string Class { get; set; } = "";

    private string StyleAttribute => DominantColorCss.ToVariableStyle("--media-dominant-color", DominantColor);
}
