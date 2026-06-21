using K7.Clients.Shared.Enums;
using K7.Clients.Shared.UI.Components;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceStatsPage
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [SupplyParameterFromQuery(Name = "tab")] public string? Tab { get; set; }

    private string _activeTab = "video";
    private List<ButtonGroupOption<string>> _tabOptions = [];

    protected override void OnInitialized()
    {
        _tabOptions =
        [
            new("video", Label: L["VideoTab"]),
            new("music", Label: L["MusicTab"])
        ];

        _activeTab = Tab == "music" ? "music" : "video";
    }

    private void OnTabChanged(string value)
    {
        _activeTab = value;
        Navigation.NavigateTo(value == "music" ? "/my-space/stats?tab=music" : "/my-space/stats", replace: true);
    }
}
