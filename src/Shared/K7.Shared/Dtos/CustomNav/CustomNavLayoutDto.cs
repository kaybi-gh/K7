using System.Text.Json.Serialization;
using K7.Shared.Enums;

namespace K7.Shared.Dtos.CustomNav;

public sealed record CustomNavLayoutDto
{
    public required bool Enabled { get; init; }
    public required CustomNavPlacement Placement { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CustomNavBarScope? BarScope { get; init; }
    public required bool BarShowIcons { get; init; }
    public string? FeedRowTitle { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public CustomNavFeedRowScope? FeedRowScope { get; init; }
    public bool ShowRowOnHome { get; init; } = true;
    public bool ShowRowOnExploreFeeds { get; init; }
    public bool ShowBarOnHome { get; init; } = true;
    public bool ShowBarOnExplore { get; init; } = true;
    public bool ShowBarOnMySpace { get; init; }
    public bool ShowBarOnSettings { get; init; }
    public bool ShowBarOnMedia { get; init; }
    public bool ShowOnDesktop { get; init; } = true;
    public bool ShowOnPhone { get; init; }
    public bool ShowOnTv { get; init; } = true;
    public required IReadOnlyList<CustomNavItemDto> Items { get; init; }

    public static CustomNavLayoutDto Disabled() => new()
    {
        Enabled = false,
        Placement = CustomNavPlacement.FeedRow,
        BarShowIcons = true,
        ShowRowOnHome = true,
        ShowRowOnExploreFeeds = false,
        ShowBarOnHome = true,
        ShowBarOnExplore = true,
        ShowBarOnMySpace = false,
        ShowBarOnSettings = false,
        ShowBarOnMedia = false,
        ShowOnDesktop = true,
        ShowOnPhone = false,
        ShowOnTv = true,
        Items = []
    };
}
