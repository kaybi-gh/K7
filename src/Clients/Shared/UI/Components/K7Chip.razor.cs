using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Chip
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public object? Value { get; set; }
    [Parameter] public string Text { get; set; } = "";
    [Parameter] public string Variant { get; set; } = "";
    [Parameter] public string Color { get; set; } = "";
    [Parameter] public string Size { get; set; } = "";
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public string Style { get; set; } = "";
    [Parameter] public EventCallback OnClick { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private async Task OnKeyDown(KeyboardEventArgs e)
    {
        if (!OnClick.HasDelegate)
            return;

        if (e.Key is "Enter" or " ")
            await OnClick.InvokeAsync();
    }
}
