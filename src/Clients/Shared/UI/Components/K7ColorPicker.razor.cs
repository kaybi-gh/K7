using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public partial class K7ColorPicker
{
    [Parameter] public string Value { get; set; } = "#FFFFFF";
    [Parameter] public EventCallback<string> ValueChanged { get; set; }
    [Parameter] public string Label { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";

    private readonly string _id = $"k7cp-{Guid.NewGuid():N}";

    private async Task OnInput(ChangeEventArgs e)
    {
        var newValue = e.Value?.ToString() ?? "#FFFFFF";
        Value = newValue;
        await ValueChanged.InvokeAsync(newValue);
    }

    private Task OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Code is "Enter" or "Space")
        {
            // Native color input opens on click; Enter/Space triggers it via the browser
        }

        return Task.CompletedTask;
    }
}
