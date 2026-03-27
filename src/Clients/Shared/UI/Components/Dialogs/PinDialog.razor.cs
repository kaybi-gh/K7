using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class PinDialog
{
    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter]
    private IMudDialogInstance MudDialog { get; set; } = null!;

    [Parameter]
    public string UserName { get; set; } = "";

    private string _pin = "";
    private MudTextField<string>? _pinField;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Task.Delay(300);
            if (_pinField is not null)
                await _pinField.FocusAsync();
        }
    }

    private void Submit() => MudDialog.Close(DialogResult.Ok(_pin));
    private void Cancel() => MudDialog.Cancel();

    private void OnKeyUp(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && !string.IsNullOrEmpty(_pin))
            Submit();
    }
}
