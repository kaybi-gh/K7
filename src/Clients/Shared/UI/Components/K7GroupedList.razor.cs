using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Components;

public sealed record K7GroupedListGroup<TItem>
{
    public required string Key { get; init; }
    public required string Label { get; init; }
    public required IReadOnlyList<TItem> Items { get; init; }
}

public partial class K7GroupedList<TItem>
{
    [Parameter] public IReadOnlyList<K7GroupedListGroup<TItem>> Groups { get; set; } = [];
    [Parameter] public Func<TItem, string> ItemLabel { get; set; } = item => item?.ToString() ?? "";
    [Parameter] public Func<TItem, string?>? ItemDescription { get; set; }
    [Parameter] public Func<TItem, IEnumerable<string>>? ItemKeywords { get; set; }
    [Parameter] public RenderFragment<TItem>? ItemTemplate { get; set; }
    [Parameter] public EventCallback<TItem> ItemClicked { get; set; }
    [Parameter] public bool Searchable { get; set; }
    [Parameter] public string SearchPlaceholder { get; set; } = "";
    [Parameter] public Func<TItem, object>? ItemKeySelector { get; set; }
    [Parameter] public string Class { get; set; } = "";

    private string _search = "";

    private IReadOnlyList<K7GroupedListGroup<TItem>> VisibleGroups
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_search))
                return Groups;

            var query = _search.Trim();
            return Groups
                .Select(g => new K7GroupedListGroup<TItem>
                {
                    Key = g.Key,
                    Label = g.Label,
                    Items = g.Items.Where(item => Matches(item, query)).ToList()
                })
                .Where(g => g.Items.Count > 0)
                .ToList();
        }
    }

    private bool Matches(TItem item, string query)
    {
        if (ItemLabel(item).Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        var description = ItemDescription?.Invoke(item);
        if (!string.IsNullOrWhiteSpace(description)
            && description.Contains(query, StringComparison.OrdinalIgnoreCase))
            return true;

        if (ItemKeywords is not null
            && ItemKeywords(item).Any(k => k.Contains(query, StringComparison.OrdinalIgnoreCase)))
            return true;

        return false;
    }

    private object ItemKey(TItem item) => ItemKeySelector?.Invoke(item) ?? item!;

    private void OnSearchChanged(string? value)
    {
        _search = value ?? "";
    }

    private async Task OnItemClicked(TItem item)
    {
        await ItemClicked.InvokeAsync(item);
    }

    private async Task OnItemKeyDown(KeyboardEventArgs e, TItem item)
    {
        if (e.Key is "Enter" or " ")
            await OnItemClicked(item);
    }
}
