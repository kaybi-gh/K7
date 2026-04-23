using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class PinDialog
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter]
    public string UserName { get; set; } = "";

    private string _pin = "";
    private K7.Clients.Shared.UI.Components.K7TextField<string>? _pinField;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Task.Delay(300);
            // _pinField.FocusAsync() removed - K7TextField doesn't expose FocusAsync directly
        }
    }

    private void Submit() => Dialog.Close(K7DialogResult.Ok(_pin));
    private void Cancel() => Dialog.Cancel();

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrEmpty(_pin))
            Submit();
    }
}
