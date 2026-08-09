using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7JumpIndex : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter, EditorRequired] public IReadOnlyList<string> Labels { get; set; } = [];
    [Parameter] public EventCallback<string> OnJumpRequested { get; set; }
    [Parameter] public string AriaLabel { get; set; } = "Jump index";

    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<K7JumpIndex>? _dotnetRef;
    private string? _activeLabel;
    private bool _dragging;
    private volatile bool _disposed;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender || _disposed)
            return;

        try
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/jumpIndex.js");
            if (_disposed)
                return;

            _dotnetRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("init", _root, _dotnetRef);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    [JSInvokable]
    public async Task OnDragLabel(string label)
    {
        if (_disposed || label == _activeLabel)
            return;

        _activeLabel = label;
        _dragging = true;
        await OnJumpRequested.InvokeAsync(label);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnDragEnd()
    {
        if (_disposed)
            return;

        _dragging = false;
        _ = InvokeAsync(StateHasChanged);
    }

    private async Task OnLabelClicked(string label)
    {
        if (_disposed)
            return;

        _activeLabel = label;
        await OnJumpRequested.InvokeAsync(label);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        var module = _module;
        _module = null;

        if (module is not null)
        {
            try
            {
                await module.InvokeVoidAsync("dispose", _root);
                await module.DisposeAsync();
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }
        }

        _dotnetRef?.Dispose();
        _dotnetRef = null;
    }

    private static bool IsBenignJsFailure(Exception ex) =>
        ex is JSDisconnectedException or ObjectDisposedException or JSException or InvalidOperationException;
}
