using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class K7SortableList<TItem> : IAsyncDisposable
{
    private readonly string _jsId = Guid.NewGuid().ToString("N");
    private ElementReference _listRef;
    private IJSObjectReference? _module;
    private DotNetObjectReference<K7SortableList<TItem>>? _dotnetRef;
    private bool _attached;
    private bool _disposed;
    private object? _grabKey;
    private List<TItem>? _grabSnapshot;
    private bool _focusGrabbed;

    [Parameter, EditorRequired] public List<TItem> Items { get; set; } = [];
    [Parameter] public RenderFragment<TItem>? ChildContent { get; set; }
    [Parameter] public bool Disabled { get; set; }
    [Parameter] public EventCallback Reordered { get; set; }
    [Parameter] public Func<TItem, object>? ItemKey { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return;

        await EnsureModuleAsync();
        if (_disposed || _module is null)
            return;

        if (Disabled)
        {
            await DetachAsync();
            return;
        }

        if (!_attached)
        {
            _dotnetRef ??= DotNetObjectReference.Create(this);
            try
            {
                await _module.InvokeVoidAsync("attach", _jsId, _listRef, _dotnetRef);
                _attached = true;
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }
        }

        if (!_focusGrabbed)
            return;

        _focusGrabbed = false;
        try
        {
            await _module.InvokeVoidAsync("focusGrabbed", _jsId);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    [JSInvokable]
    public Task OnDragDropped(int from, int to) => InvokeAsync(() => MoveAsync(from, to));

    [JSInvokable]
    public Task OnKeyboardGrabStart(int index) => InvokeAsync(() =>
    {
        if (Disabled || index < 0 || index >= Items.Count || _grabKey is not null)
            return;

        _grabKey = GetKey(Items[index], index);
        _grabSnapshot = [.. Items];
        StateHasChanged();
    });

    [JSInvokable]
    public Task OnKeyboardGrabCommit() => InvokeAsync(() =>
    {
        _grabKey = null;
        _grabSnapshot = null;
        StateHasChanged();
    });

    [JSInvokable]
    public Task OnKeyboardGrabCancel() => InvokeAsync(CancelGrabAsync);

    private async Task OnHandleKeyDownAsync(KeyboardEventArgs e, int index)
    {
        if (Disabled || _grabKey is null)
            return;

        if (e.Key == "ArrowUp" && index > 0)
            await SwapAsync(index, index - 1);
        else if (e.Key == "ArrowDown" && index < Items.Count - 1)
            await SwapAsync(index, index + 1);
    }

    private bool IsGrabbing(TItem item, int index) =>
        _grabKey is not null && Equals(GetKey(item, index), _grabKey);

    private object GetKey(TItem item, int index) => ItemKey?.Invoke(item) ?? index;

    private async Task MoveAsync(int from, int to)
    {
        if (_disposed || Disabled || from < 0 || from >= Items.Count)
            return;

        var insertAt = to;
        if (insertAt < 0)
            insertAt = 0;
        if (insertAt > Items.Count)
            insertAt = Items.Count;
        if (from < insertAt)
            insertAt--;
        if (insertAt == from || insertAt < 0)
            return;

        var item = Items[from];
        Items.RemoveAt(from);
        if (insertAt > Items.Count)
            insertAt = Items.Count;
        Items.Insert(insertAt, item);
        await Reordered.InvokeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task SwapAsync(int from, int to)
    {
        if (_disposed || Disabled)
            return;

        var item = Items[from];
        Items.RemoveAt(from);
        Items.Insert(to, item);
        _focusGrabbed = true;
        await Reordered.InvokeAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task CancelGrabAsync()
    {
        if (_grabSnapshot is not null)
        {
            var changed = _grabSnapshot.Count != Items.Count
                || !_grabSnapshot.SequenceEqual(Items);
            Items.Clear();
            Items.AddRange(_grabSnapshot);
            if (changed)
                await Reordered.InvokeAsync();
        }

        _grabKey = null;
        _grabSnapshot = null;
        _focusGrabbed = true;
        StateHasChanged();
    }

    private async Task EnsureModuleAsync()
    {
        if (_module is not null)
            return;

        try
        {
            _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/k7SortableList.js");
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    private async Task DetachAsync()
    {
        if (!_attached || _module is null)
        {
            _attached = false;
            return;
        }

        _attached = false;
        try
        {
            await _module.InvokeVoidAsync("dispose", _jsId);
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;
        await DetachAsync();

        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
            }

            _module = null;
        }

        _dotnetRef?.Dispose();
        _dotnetRef = null;
    }

    private static bool IsBenignJsFailure(Exception ex) =>
        ex is JSDisconnectedException or ObjectDisposedException or JSException or InvalidOperationException;
}

internal sealed class K7SortableListStrings;
