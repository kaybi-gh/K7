using K7.Clients.Shared.Enums;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web.Virtualization;

namespace K7.Clients.Shared.UI.Components;

[CascadingTypeParameter(nameof(TItem))]
public partial class K7DataTable<TItem>
{
    [Parameter] public IList<TItem>? Items { get; set; }
    [Parameter] public Func<K7DataTableState<TItem>, CancellationToken, Task<K7DataTableResult<TItem>>>? ServerData { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string? ActiveSortKey { get; set; }
    [Parameter] public K7SortDirection ActiveSortDirection { get; set; } = K7SortDirection.Ascending;
    [Parameter] public EventCallback<SortChangedEventArgs> OnSortChanged { get; set; }
    [Parameter] public EventCallback<TItem> OnRowClick { get; set; }
    [Parameter] public string? PersistenceKey { get; set; }
    [Parameter] public float RowHeight { get; set; } = 48;
    [Parameter] public int OverscanCount { get; set; } = 10;

    private readonly List<K7DataColumn<TItem>> _columns = [];
    private Virtualize<TItem>? _virtualizeRef;
    private CancellationTokenSource? _cts;
    private bool _columnPickerOpen;

    internal void AddColumn(K7DataColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            StateHasChanged();
        }
    }

    internal void RemoveColumn(K7DataColumn<TItem> column)
    {
        _columns.Remove(column);
        StateHasChanged();
    }

    public async Task RefreshAsync()
    {
        if (_virtualizeRef is not null)
        {
            await _virtualizeRef.RefreshDataAsync();
        }
    }

    private async ValueTask<ItemsProviderResult<TItem>> ProvideItemsAsync(
        ItemsProviderRequest request)
    {
        if (ServerData is null) return default;

        _cts?.Cancel();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(request.CancellationToken);

        try
        {
            var state = new K7DataTableState<TItem>(
                request.StartIndex,
                request.Count,
                ActiveSortKey,
                ActiveSortDirection);

            var result = await ServerData(state, _cts.Token);
            return new ItemsProviderResult<TItem>(result.Items, result.TotalItemCount);
        }
        catch (OperationCanceledException)
        {
            return default;
        }
    }

    private async Task OnHeaderClick(K7DataColumn<TItem> column)
    {
        if (column.SortKey is null) return;

        var direction = column.SortKey == ActiveSortKey
            ? ActiveSortDirection is K7SortDirection.Ascending
                ? K7SortDirection.Descending
                : K7SortDirection.Ascending
            : K7SortDirection.Ascending;

        await OnSortChanged.InvokeAsync(new SortChangedEventArgs(column.SortKey, direction));
        await RefreshAsync();
    }

    private async Task OnRowClicked(TItem item)
    {
        await OnRowClick.InvokeAsync(item);
    }

    private void ToggleColumnVisibility(K7DataColumn<TItem> column)
    {
        column.SetVisible(!column.IsVisible);
        StateHasChanged();
    }

    private string GetSortIcon(K7DataColumn<TItem> column)
    {
        if (column.SortKey is null || column.SortKey != ActiveSortKey)
        {
            return Phosphor.CaretUpDown;
        }

        return ActiveSortDirection is K7SortDirection.Ascending
            ? Phosphor.SortAscending
            : Phosphor.SortDescending;
    }

    private async Task OnRowKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e, TItem item)
    {
        if (e.Key is "Enter" or " ")
        {
            await OnRowClicked(item);
        }
    }

    private IEnumerable<K7DataColumn<TItem>> VisibleColumns =>
        _columns.Where(c => c.IsVisible);
}
