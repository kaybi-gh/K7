using K7.Clients.Shared.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class K7DataColumn<TItem>
{
    [CascadingParameter] public K7DataTable<TItem> DataTable { get; set; } = default!;

    [Parameter, EditorRequired] public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Stable identifier used to remember the column visibility. Defaults to the sort key,
    /// then the title (which changes with the language).
    /// </summary>
    [Parameter] public string? Key { get; set; }
    [Parameter] public string? SortKey { get; set; }
    [Parameter] public string? Width { get; set; }
    [Parameter] public bool Hideable { get; set; } = true;
    [Parameter] public bool Visible { get; set; } = true;
    [Parameter] public bool VisibleOnMobile { get; set; } = true;
    [Parameter] public RenderFragment<TItem>? CellTemplate { get; set; }
    [Parameter] public Func<TItem, object?>? Property { get; set; }
    [Parameter] public RenderFragment? FilterTemplate { get; set; }

    internal bool IsVisible { get; set; } = true;

    internal string ColumnKey => Key ?? SortKey ?? Title;

    /// <summary>The Visible parameter, as opposed to the picker state in IsVisible.</summary>
    internal bool VisibleParameter => _visibleParameter;

    private bool _visibleParameter = true;

    protected override void OnInitialized()
    {
        _visibleParameter = Visible;
        IsVisible = Visible;
        DataTable.AddColumn(this);
    }

    protected override void OnParametersSet()
    {
        // Only react when the parent changes Visible. Comparing with IsVisible re-showed a
        // column on the very render that followed its toggle in the column picker.
        if (_visibleParameter == Visible)
            return;

        _visibleParameter = Visible;
        IsVisible = Visible && !(Hideable && DataTable.IsStoredHidden(ColumnKey));
        DataTable.InvalidateLayout();
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
