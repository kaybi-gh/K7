using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Models;
using K7.Shared.Dtos.Entities.Reviews;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class MediaReviewsSection : ComponentBase
{
    [Inject] private IReviewService ReviewService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;

    [Parameter] public Guid MediaId { get; set; }

    private Carousel? _carousel;
    private readonly List<MediaReviewCardModel> _reviews = [];
    private Guid? _currentUserId;
    private bool _blurUnwatchedMedia;
    private ReviewPreferencesDto _reviewPreferences = new();

    public async Task RefreshAsync()
    {
        var localReviews = await ReviewService.GetMediaReviewsAsync(MediaId);
        var federatedReviews = await ReviewService.GetFederatedMediaReviewsAsync(MediaId);

        _reviews.Clear();
        _reviews.AddRange(localReviews.Select(MediaReviewCardModel.FromLocal));
        _reviews.AddRange(federatedReviews.Select(MediaReviewCardModel.FromFederated));
        _reviews.Sort((a, b) => b.Created.CompareTo(a.Created));

        await UpdateSpoilerStateAsync();
        await InvokeAsync(StateHasChanged);

        if (_carousel is not null)
            await _carousel.NotifyItemsChangedAsync();
    }

    protected override async Task OnParametersSetAsync()
    {
        await RefreshAsync();
    }

    private async Task UpdateSpoilerStateAsync()
    {
        var me = await UserAdminService.GetCurrentUserAsync();
        _currentUserId = me?.Id;

        _reviewPreferences = await ReviewService.GetReviewPreferencesAsync();
        if (!_reviewPreferences.BlurReviewsForUnwatchedMedia)
        {
            _blurUnwatchedMedia = false;
            return;
        }

        var media = await MediaService.GetMediaAsync(MediaId);
        var state = media?.UserState;
        var watched = state is not null && (state.IsCompleted || state.PlayCount > 0);
        _blurUnwatchedMedia = !watched;
    }

    private bool IsReviewSpoilerBlurred(MediaReviewCardModel review) =>
        _blurUnwatchedMedia && review.UserId != _currentUserId;

    private Task OpenReviewAsync(MediaReviewCardModel review) =>
        MediaReviewDetailDialogHelper.OpenAsync(DialogService, DetailDialogL, review);
}
