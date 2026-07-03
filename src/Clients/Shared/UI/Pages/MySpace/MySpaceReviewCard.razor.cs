using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceReviewCard
{
    [Parameter, EditorRequired] public SocialUserReviewViewDto Review { get; set; } = default!;
    [Parameter] public bool Compact { get; set; }
    [Parameter, EditorRequired] public EventCallback OnEdit { get; set; }
    [Parameter, EditorRequired] public EventCallback OnDelete { get; set; }
}
