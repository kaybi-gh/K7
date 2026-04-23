using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7ProgressBar
{
    [Parameter] public double Value { get; set; }
    [Parameter] public bool Indeterminate { get; set; }
    [Parameter] public string Class { get; set; } = "";
}
