using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Clients.Shared.Models;

public sealed class CustomNavLayoutEditModel
{
    public bool Enabled { get; set; }
    public CustomNavPlacement Placement { get; set; } = CustomNavPlacement.FeedRow;
    public bool BarShowIcons { get; set; } = true;
    public string FeedRowTitle { get; set; } = "";
    public bool ShowRowOnHome { get; set; } = true;
    public bool ShowRowOnExploreFeeds { get; set; }
    public bool ShowBarOnHome { get; set; } = true;
    public bool ShowBarOnExplore { get; set; } = true;
    public bool ShowBarOnMySpace { get; set; }
    public bool ShowBarOnSettings { get; set; }
    public bool ShowBarOnMedia { get; set; }
    public bool ShowOnDesktop { get; set; } = true;
    public bool ShowOnPhone { get; set; }
    public bool ShowOnTv { get; set; } = true;
    public List<CustomNavItemEditModel> Items { get; set; } = [];

    public static CustomNavLayoutEditModel FromDto(CustomNavLayoutDto layout) => new()
    {
        Enabled = layout.Enabled,
        Placement = layout.Placement,
        BarShowIcons = layout.BarShowIcons,
        FeedRowTitle = layout.FeedRowTitle ?? "",
        ShowRowOnHome = layout.ShowRowOnHome,
        ShowRowOnExploreFeeds = layout.ShowRowOnExploreFeeds,
        ShowBarOnHome = layout.ShowBarOnHome,
        ShowBarOnExplore = layout.ShowBarOnExplore,
        ShowBarOnMySpace = layout.ShowBarOnMySpace,
        ShowBarOnSettings = layout.ShowBarOnSettings,
        ShowBarOnMedia = layout.ShowBarOnMedia,
        ShowOnDesktop = layout.ShowOnDesktop,
        ShowOnPhone = layout.Placement != CustomNavPlacement.Bar && layout.ShowOnPhone,
        ShowOnTv = layout.ShowOnTv,
        Items = layout.Items.Select(CustomNavItemEditModel.FromDto).ToList()
    };

    public CustomNavLayoutDto ToDto() => new()
    {
        Enabled = Enabled,
        Placement = Placement,
        BarShowIcons = BarShowIcons,
        FeedRowTitle = string.IsNullOrWhiteSpace(FeedRowTitle) ? null : FeedRowTitle.Trim(),
        ShowRowOnHome = ShowRowOnHome,
        ShowRowOnExploreFeeds = ShowRowOnExploreFeeds,
        ShowBarOnHome = ShowBarOnHome,
        ShowBarOnExplore = ShowBarOnExplore,
        ShowBarOnMySpace = ShowBarOnMySpace,
        ShowBarOnSettings = ShowBarOnSettings,
        ShowBarOnMedia = ShowBarOnMedia,
        ShowOnDesktop = ShowOnDesktop,
        ShowOnPhone = Placement == CustomNavPlacement.FeedRow && ShowOnPhone,
        ShowOnTv = ShowOnTv,
        Items = Items.Select(i => i.ToDto()).ToList()
    };
}
