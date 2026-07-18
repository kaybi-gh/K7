using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class LibraryTableMediaTitle
{
    [Parameter] public string? ImageUrl { get; set; }
    [Parameter] public string? MediaType { get; set; }
    [Parameter] public string? Title { get; set; }
    [Parameter] public string? Href { get; set; }
    [Parameter] public bool StopPropagation { get; set; }
    [Parameter] public string LinkClass { get; set; } = "text-sm";
    [Parameter] public string TextClass { get; set; } = "text-sm";
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public TableMediaThumbHelper.Size ThumbSize { get; set; } = TableMediaThumbHelper.Size.Standard;
}
