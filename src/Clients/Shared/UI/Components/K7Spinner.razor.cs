using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7Spinner
{
    [Parameter] public string Size { get; set; } = "";
    [Parameter] public string Class { get; set; } = "";
}
