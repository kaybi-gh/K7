using K7.Clients.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7DataColumn<TItem>
{
    [CascadingParameter] public K7DataTable<TItem> DataTable { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;
    [Parameter] public string? SortKey { get; set; }
    [Parameter] public string? Width { get; set; }
    [Parameter] public bool Hideable { get; set; } = true;
    [Parameter] public bool Visible { get; set; } = true;
    [Parameter] public bool VisibleOnMobile { get; set; } = true;
    [Parameter] public RenderFragment<TItem>? CellTemplate { get; set; }
    [Parameter] public Func<TItem, object?>? Property { get; set; }
    [Parameter] public RenderFragment? FilterTemplate { get; set; }

    internal bool IsVisible { get; set; } = true;

    protected override void OnInitialized()
    {
        IsVisible = Visible;
        DataTable.AddColumn(this);
    }

    internal void SetVisible(bool visible)
    {
        IsVisible = visible;
    }

    internal RenderFragment? RenderCell(TItem item)
    {
        if (CellTemplate is not null)
        {
            return CellTemplate(item);
        }

        if (Property is not null)
        {
            var value = Property(item);
            return builder =>
            {
                builder.AddContent(0, value?.ToString() ?? "");
            };
        }

        return null;
    }

    public void Dispose()
    {
        DataTable.RemoveColumn(this);
    }
}
