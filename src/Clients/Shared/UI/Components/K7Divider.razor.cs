using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Divider
{
    [Parameter] public bool Vertical { get; set; }
    [Parameter] public string Class { get; set; } = "";
}
