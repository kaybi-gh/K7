using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public sealed class TableRowClickEventArgs<T>
{
    public T Item { get; init; } = default!;
}

public partial class K7Table<TItem>
{
    [Parameter] public IEnumerable<TItem>? Items { get; set; }
    [Parameter] public RenderFragment? HeaderContent { get; set; }
    [Parameter] public RenderFragment<TItem>? RowTemplate { get; set; }
    [Parameter] public bool Dense { get; set; }
    [Parameter] public bool Hover { get; set; }
    [Parameter] public string RowClass { get; set; } = "";
    [Parameter] public EventCallback<TableRowClickEventArgs<TItem>> OnRowClick { get; set; }
    [Parameter] public string Class { get; set; } = "";
}
