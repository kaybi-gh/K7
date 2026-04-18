using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Complex;

public partial class K7TabPanel
{
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Text { get; set; } = "";
    [Parameter] public string Icon { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";

    [CascadingParameter] internal K7Tabs? Parent { get; set; }

    protected override void OnInitialized()
    {
        Parent?.Register(this);
    }
}
