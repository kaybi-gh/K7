using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public sealed record K7AvatarGroupItem
{
    public Guid? UserId { get; init; }
    public string? Image { get; init; }
    public string? Letter { get; init; }
}

public partial class K7AvatarGroup
{
    [Parameter] public IReadOnlyList<K7AvatarGroupItem> Members { get; set; } = [];
    [Parameter] public string Size { get; set; } = "";
    [Parameter] public int MaxVisible { get; set; } = 3;
    [Parameter] public string Class { get; set; } = "";
    [Parameter] public RenderFragment? ChildContent { get; set; }

    private IEnumerable<K7AvatarGroupItem> VisibleMembers =>
        Members.Take(MaxVisible > 0 ? MaxVisible : 3);
}
