using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class K7IconPickerDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;

    [Parameter] public string? InitialValue { get; set; }
    [Parameter] public string SearchPlaceholder { get; set; } = "Search an icon...";
    [Parameter] public string CancelText { get; set; } = "Cancel";
    [Parameter] public string ConfirmText { get; set; } = "Select";

    private string? _selected;

    protected override void OnParametersSet()
    {
        _selected = InitialValue;
    }

    private void Confirm() => Dialog.Close(K7DialogResult.Ok(_selected));
    private void Cancel() => Dialog.Cancel();
}
