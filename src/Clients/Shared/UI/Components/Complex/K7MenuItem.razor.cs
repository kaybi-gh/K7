using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Complex;

public partial class K7MenuItem
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public string Class { get; set; } = "";

    [CascadingParameter] private K7Menu? ParentMenu { get; set; }

    private async Task OnItemClick()
    {
        ParentMenu?.Close();
        await OnClick.InvokeAsync();
    }
}
