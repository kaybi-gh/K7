using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Tabs<TValue>
{
    [Parameter, EditorRequired] public IReadOnlyList<TabOption<TValue>> Tabs { get; set; } = [];
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public string? Class { get; set; }

    private async Task OnTabClicked(TValue value)
    {
        if (EqualityComparer<TValue>.Default.Equals(value, Value)) return;
        await ValueChanged.InvokeAsync(value);
    }
}
