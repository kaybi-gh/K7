using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class K7FilterMenu
{
    [Inject] private IStringLocalizer<K7FilterMenu> L { get; set; } = default!;

    [Parameter] public bool HasActiveFilters { get; set; }
    [Parameter] public string? ActiveLabel { get; set; }
    [Parameter] public EventCallback OnClear { get; set; }
    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public string Class { get; set; } = string.Empty;

    private bool _menuOpen;
    private readonly K7FilterMenuContext _context = new();

    private string DisplayLabel => HasActiveFilters && !string.IsNullOrWhiteSpace(ActiveLabel)
        ? ActiveLabel!
        : L["Filters"];

    private void OnMenuOpenChanged(bool open)
    {
        _menuOpen = open;
        if (!open)
        {
            _context.Reset();
        }
    }

    protected override void OnInitialized()
    {
        _context.Register(() => InvokeAsync(StateHasChanged));
    }

    private async Task ClearFiltersAsync()
    {
        _menuOpen = false;
        _context.Reset();
        await OnClear.InvokeAsync();
    }
}
