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
    [Parameter] public bool ShowToolbar { get; set; } = true;
    [Parameter] public string? Height { get; set; }

    private readonly List<K7DataColumn<TItem>> _columns = [];
    private Virtualize<TItem>? _virtualizeRef;
    private bool _columnPickerOpen;
    private bool _needsRender = true;
    private string? _prevSortKey;
    private K7SortDirection _prevSortDirection;

    protected override bool ShouldRender()
    {
        if (_prevSortKey != ActiveSortKey || _prevSortDirection != ActiveSortDirection)
        {
            _prevSortKey = ActiveSortKey;
            _prevSortDirection = ActiveSortDirection;
            _needsRender = true;
        }

        if (!_needsRender)
        {
            return false;
        }
        _needsRender = false;
        return true;
    }

    public void ToggleColumnPicker()
    {
        _columnPickerOpen = !_columnPickerOpen;
        _needsRender = true;
        StateHasChanged();
    }

    internal void AddColumn(K7DataColumn<TItem> column)
    {
        if (!_columns.Contains(column))
        {
            _columns.Add(column);
            _needsRender = true;
            StateHasChanged();
        }
    }

    internal void RemoveColumn(K7DataColumn<TItem> column)
    {
        _columns.Remove(column);
    }

    public async Task RefreshAsync()
    {
        if (_virtualizeRef is not null)
        {
            _needsRender = true;
            await _virtualizeRef.RefreshDataAsync();
        }
    }

    private async ValueTask<ItemsProviderResult<TItem>> ProvideItemsAsync(
        ItemsProviderRequest request)
    {
        if (ServerData is null) return default;

        var state = new K7DataTableState<TItem>(
            request.StartIndex,
            request.Count,
            ActiveSortKey,
            ActiveSortDirection);

        try
        {
            var result = await ServerData(state, request.CancellationToken);
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
        _needsRender = true;
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

    private string? ScrollStyle => Height is not null ? $"height: {Height}; flex: none" : null;
}
