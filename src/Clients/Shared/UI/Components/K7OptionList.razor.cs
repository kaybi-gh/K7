using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// Single choice among a few options, all visible at once. Prefer it over a select for short
/// lists on settings pages: one remote press picks an option, with no popup to open.
/// </summary>
public partial class K7OptionList<TValue>
{
    [Parameter, EditorRequired] public IReadOnlyList<ButtonGroupOption<TValue>> Options { get; set; } = [];
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public EventCallback<TValue> ValueChanged { get; set; }
    [Parameter] public string? AriaLabel { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public string? Class { get; set; }

    private async Task OnOptionClicked(TValue value)
    {
        if (EqualityComparer<TValue>.Default.Equals(value, Value))
            return;

        await ValueChanged.InvokeAsync(value);
    }
}
