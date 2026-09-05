using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public partial class VerticalCarousel : IAsyncDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private ElementReference _root;
    private IJSObjectReference? _module;
    private DotNetObjectReference<VerticalCarousel>? _dotnetRef;
    private bool _moduleLoadFailed;
    private int _lastSlideCount = -1;
    private volatile bool _disposed;
    private TvVerticalWindow? _rowWindow;
    private bool _hasUserMoved;
    private int _appliedInitialIndex = int.MinValue;

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public bool VirtualizeRows { get; set; }
    [Parameter] public int InitialActiveIndex { get; set; }

    protected override void OnParametersSet()
    {
        if (!VirtualizeRows)
            return;

        if (_rowWindow is null)
        {
            _rowWindow = new TvVerticalWindow();
            _rowWindow.Reset(InitialActiveIndex);
            _appliedInitialIndex = InitialActiveIndex;
            return;
        }

        if (!_hasUserMoved && InitialActiveIndex != _appliedInitialIndex)
        {
            _rowWindow.Reset(InitialActiveIndex);
            _appliedInitialIndex = InitialActiveIndex;
        }
    }

    [JSInvokable]
    public Task OnActiveRowChanged(int index)
    {
        if (_disposed || _rowWindow is null)
            return Task.CompletedTask;

        _hasUserMoved = true;
        // D-pad must not re-render the feed. Only mount newly visited rows.
        if (!_rowWindow.GrowTo(index))
            return Task.CompletedTask;

        return InvokeAsync(StateHasChanged);
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed)
            return;

        if (_module is null && !_moduleLoadFailed)
        {
            try
            {
                _module = await JSRuntime.InvokeAsync<IJSObjectReference>(
                    "import", "./_content/K7.Clients.Shared.UI/js/vertical-carousel.js");
            }
            catch (Exception ex) when (IsBenignJsFailure(ex))
            {
                _moduleLoadFailed = true;
                return;
            }
        }

        if (_disposed || _module is null)
            return;

        try
        {
            if (firstRender)
            {
                if (VirtualizeRows)
                    _dotnetRef = DotNetObjectReference.Create(this);

                await _module.InvokeVoidAsync("init", _root, _dotnetRef);
                if (_disposed)
                    return;
                _lastSlideCount = await _module.InvokeAsync<int>("getSlideCount", _root);
            }
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
        var module = _module;
        _module = null;
        var dotnetRef = _dotnetRef;
        _dotnetRef = null;
        dotnetRef?.Dispose();

        if (module is null)
            return;

        try
        {
            await module.InvokeVoidAsync("destroy", _root);
            await module.DisposeAsync();
        }
        catch (Exception ex) when (IsBenignJsFailure(ex))
        {
        }
    }

    private static bool IsBenignJsFailure(Exception ex) =>
        ex is JSDisconnectedException or ObjectDisposedException or JSException or InvalidOperationException;
}
