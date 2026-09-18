using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components;

public sealed class TableRowClickEventArgs<T>
{
    public T Item { get; init; } = default!;
}

public partial class K7Table<TItem> : IAsyncDisposable
{
    private ElementReference _tableRef;
    private IJSObjectReference? _browseViewModule;
    private bool _keyNavInitialized;
    private bool _disposed;

    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    [Parameter] public IEnumerable<TItem>? Items { get; set; }
    [Parameter] public RenderFragment? HeaderContent { get; set; }
    [Parameter] public RenderFragment<TItem>? RowTemplate { get; set; }
    [Parameter] public bool Dense { get; set; }
    [Parameter] public bool Hover { get; set; }
    [Parameter] public string RowClass { get; set; } = "";
    [Parameter] public Func<TItem, string?>? RowId { get; set; }
    [Parameter] public EventCallback<TableRowClickEventArgs<TItem>> OnRowClick { get; set; }
    [Parameter] public string Class { get; set; } = "";

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_disposed || _keyNavInitialized || Items is null)
            return;

        try
        {
            _browseViewModule ??= await JSRuntime.InvokeAsync<IJSObjectReference>(
                "import", "./_content/K7.Clients.Shared.UI/js/browseView.js");
            await _browseViewModule.InvokeVoidAsync("initTableKeyNav", _tableRef, 48f);
            _keyNavInitialized = true;
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        _disposed = true;
        if (_browseViewModule is null)
            return;

        try
        {
            if (_keyNavInitialized)
                await _browseViewModule.InvokeVoidAsync("disposeTableKeyNav", _tableRef);
            await _browseViewModule.DisposeAsync();
        }
        catch (Exception ex) when (ex is JSDisconnectedException or ObjectDisposedException or JSException)
        {
        }

        _browseViewModule = null;
    }
}
