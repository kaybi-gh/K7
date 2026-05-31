using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public partial class K7CheckboxList<TItem>
{
    [Parameter] public IReadOnlyList<TItem> Items { get; set; } = [];
    [Parameter] public Func<TItem, bool> IsChecked { get; set; } = _ => false;
    [Parameter] public EventCallback<(TItem Item, bool Checked)> CheckedChanged { get; set; }
    [Parameter] public Func<TItem, string> ItemLabel { get; set; } = item => item?.ToString() ?? "";
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private bool ShouldPreventKey => true;

    private async Task OnToggle(TItem item, bool value)
    {
        await CheckedChanged.InvokeAsync((item, value));
    }

    private async Task OnKeyDown(KeyboardEventArgs e, TItem item, bool currentValue)
    {
        if (e.Key is "Enter" or " ")
        {
            await OnToggle(item, !currentValue);
        }
    }
}
