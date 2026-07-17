using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7ButtonGroup<TValue>
{
    [Parameter, EditorRequired] public IReadOnlyList<ButtonGroupOption<TValue>> Options { get; set; } = [];
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public string? Class { get; set; }

    private async Task OnOptionClicked(TValue value)
    {
        if (EqualityComparer<TValue>.Default.Equals(value, Value)) return;
        await ValueChanged.InvokeAsync(value);
    }
}
