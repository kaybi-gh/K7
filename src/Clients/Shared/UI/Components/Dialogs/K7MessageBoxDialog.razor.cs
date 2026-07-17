using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class K7MessageBoxDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public string Message { get; set; } = "";
    [Parameter] public string YesText { get; set; } = "OK";
    [Parameter] public string? NoText { get; set; }
    [Parameter] public string? CancelText { get; set; }
}
