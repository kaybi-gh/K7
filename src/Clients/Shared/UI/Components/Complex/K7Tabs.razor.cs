using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Complex;

public partial class K7Tabs
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public int ActivePanelIndex { get; set; }
    [Parameter] public EventCallback<int> ActivePanelIndexChanged { get; set; }
    [Parameter] public string Class { get; set; } = "";

    internal readonly List<K7TabPanel> _panels = [];
    private int _activeIndex;

    protected override void OnParametersSet()
    {
        _activeIndex = ActivePanelIndex;
    }

    internal void Register(K7TabPanel panel)
    {
        if (!_panels.Contains(panel))
        {
            _panels.Add(panel);
            StateHasChanged();
        }
    }

    internal bool IsActive(K7TabPanel panel) => _panels.IndexOf(panel) == _activeIndex;

    private async Task Activate(K7TabPanel panel)
    {
        _activeIndex = _panels.IndexOf(panel);
        await ActivePanelIndexChanged.InvokeAsync(_activeIndex);
    }
}
