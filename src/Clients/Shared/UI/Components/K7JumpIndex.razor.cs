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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/jumpIndex.js");
            _dotnetRef = DotNetObjectReference.Create(this);
            await _module.InvokeVoidAsync("init", _root, _dotnetRef);
        }
    }

    [JSInvokable]
    public async Task OnDragLabel(string label)
    {
        if (label == _activeLabel) return;
        _activeLabel = label;
        _dragging = true;
        await OnJumpRequested.InvokeAsync(label);
        await InvokeAsync(StateHasChanged);
    }

    [JSInvokable]
    public void OnDragEnd()
    {
        _dragging = false;
        InvokeAsync(StateHasChanged);
    }

    private async Task OnLabelClicked(string label)
    {
        _activeLabel = label;
        await OnJumpRequested.InvokeAsync(label);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("dispose", _root);
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
        _dotnetRef?.Dispose();
    }
}
