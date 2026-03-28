using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin;

public partial class Admin
{
    private static readonly string[] TabSlugs = ["general", "libraries", "users", "devices", "restrictions", "authentication"];

    private int _activePanelIndex;

    [Parameter] public string? Tab { get; set; }

    protected override void OnParametersSet()
    {
        _activePanelIndex = GetTabIndex(Tab);
    }

    private void OnActivePanelIndexChanged(int index)
    {
        if (index == _activePanelIndex)
            return;

        _activePanelIndex = index;

        var slug = index >= 0 && index < TabSlugs.Length ? TabSlugs[index] : TabSlugs[0];
        NavigationManager.NavigateTo($"/admin/{slug}", replace: true);
    }

    private static int GetTabIndex(string? tab)
    {
        if (string.IsNullOrEmpty(tab))
            return 0;

        var index = Array.FindIndex(TabSlugs, s => s.Equals(tab, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : 0;
    }
}
