using K7.Clients.Shared.UI.Pages;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class SerieSeasonTvCast
{
    private IReadOnlyList<PersonRoleDisplayHelper.GroupedDisplay> _items = [];

    [Inject] private IStringLocalizer<SerieSeason> L { get; set; } = default!;

    [Parameter] public IReadOnlyList<PersonRoleDisplayHelper.GroupedDisplay> Items { get; set; } = [];

    protected override void OnParametersSet()
    {
        if (!ReferenceEquals(_items, Items))
            _items = Items;
    }

    /// <summary>
    /// Update casting without a parent re-render (season carousel stays mounted).
    /// </summary>
    public void ApplyCast(IReadOnlyList<PersonRoleDisplayHelper.GroupedDisplay> items)
    {
        if (ReferenceEquals(_items, items))
            return;

        _items = items;
        StateHasChanged();
    }
}
