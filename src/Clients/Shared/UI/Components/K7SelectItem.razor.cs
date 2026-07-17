using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7SelectItem<TValue> : IDisposable
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public TValue? Value { get; set; }
    [Parameter] public string? Text { get; set; }

    [CascadingParameter] private K7Select<TValue>? ParentSelect { get; set; }

    private bool _isSelected => ParentSelect?.IsSelected(Value) == true;
    private string _displayText => Text ?? Value?.ToString() ?? "";
    private SelectItemRegistration<TValue>? _registration;

    protected override void OnInitialized()
    {
        _registration = new SelectItemRegistration<TValue>(Value, _displayText);
        ParentSelect?.RegisterItem(_registration);
    }

    protected override void OnParametersSet()
    {
        if (_registration is not null &&
            (_registration.DisplayText != _displayText ||
             !EqualityComparer<TValue>.Default.Equals(_registration.Value, Value)))
        {
            ParentSelect?.UnregisterItem(_registration);
            _registration = new SelectItemRegistration<TValue>(Value, _displayText);
            ParentSelect?.RegisterItem(_registration);
        }
    }

    private async Task OnClick()
    {
        if (ParentSelect is not null)
            await ParentSelect.SelectValueAsync(Value);
    }

    public void Dispose()
    {
        if (_registration is not null)
            ParentSelect?.UnregisterItem(_registration);
    }
}
