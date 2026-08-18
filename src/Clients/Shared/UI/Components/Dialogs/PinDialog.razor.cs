using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class PinDialog : IAsyncDisposable
{
    public const int PinLength = 4;

    private static readonly char[] Digits = ['1', '2', '3', '4', '5', '6', '7', '8', '9'];

    [Inject] private IJSRuntime JS { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter]
    public string UserName { get; set; } = "";

    private DotNetObjectReference<PinDialog>? _jsRef;
    private string _pin = "";
    private bool _submitted;
    private bool _keysAttached;

    private static IReadOnlyDictionary<string, object>? GetInitialKeyAttributes(char digit) =>
        digit == '1' ? new Dictionary<string, object> { ["data-initial-focus"] = true } : null;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _keysAttached)
            return;

        _keysAttached = true;
        _jsRef = DotNetObjectReference.Create(this);

        try
        {
            await JS.InvokeVoidAsync("K7.pinDialogKeyCapture.attach", _jsRef);
        }
        catch (JSException)
        {
        }
    }

    [JSInvokable]
    public Task OnCapturedKey(string key)
    {
        HandleKey(key);
        return InvokeAsync(StateHasChanged);
    }

    private void AppendDigit(char digit)
    {
        if (_submitted || _pin.Length >= PinLength || digit is < '0' or > '9')
            return;

        _pin += digit;
        if (_pin.Length >= PinLength)
            Submit();
    }

    private void Backspace()
    {
        if (_submitted || _pin.Length == 0)
            return;

        _pin = _pin[..^1];
    }

    private void Clear()
    {
        if (_submitted)
            return;

        _pin = "";
    }

    private void Submit()
    {
        if (_submitted || _pin.Length != PinLength)
            return;

        _submitted = true;
        Dialog.Close(K7DialogResult.Ok(_pin));
    }

    private void Cancel()
    {
        if (_submitted)
            return;

        _submitted = true;
        Dialog.Cancel();
    }

    private void HandleKey(string key)
    {
        if (_submitted)
            return;

        if (key is "Escape")
        {
            Cancel();
            return;
        }

        if (key is "Backspace")
        {
            Backspace();
            return;
        }

        if (key.Length == 1 && key[0] is >= '0' and <= '9')
            AppendDigit(key[0]);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("K7.pinDialogKeyCapture.detach");
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
        }

        _jsRef?.Dispose();
        _jsRef = null;
    }
}
