using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Shared.Dtos.Federation.Social;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class SocialUserReviewCard
{
    [Parameter, EditorRequired] public SocialUserReviewViewDto Review { get; set; } = default!;
    [Parameter] public bool Compact { get; set; }
    [Parameter] public bool IsSpoilerBlurred { get; set; }
    [Parameter] public EventCallback OnActivate { get; set; }
    [Parameter] public string? ActivateAriaLabel { get; set; }
    [Parameter] public RenderFragment? Actions { get; set; }

    private MediaCardViewModel _mediaCard = default!;
    private string? _mediaHref;
    private MediaCardVariant _mediaVariant;

    protected override void OnParametersSet()
    {
        _mediaCard = MediaReviewCardHelper.ToMediaCardViewModel(Review, S["Untitled"]);
        _mediaHref = SocialUserNavigation.GetMediaHref(Review.Media.LocalMediaId, Review.Media.Media.Type);
        _mediaVariant = MediaReviewCardHelper.GetMediaCardVariant(Review.Media.Media.Type);
    }
}
