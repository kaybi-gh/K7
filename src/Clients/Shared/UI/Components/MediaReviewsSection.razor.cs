using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Models;
using K7.Shared.Dtos.Entities;
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

    /// <summary>
    /// Watched state the page already holds (see <see cref="IsWatched"/>). When set, spoiler blur
    /// does not reload the whole media just to read it.
    /// </summary>
    [Parameter] public bool? Watched { get; set; }

    private Carousel? _carousel;
    private readonly List<MediaReviewCardModel> _reviews = [];
    private Guid? _currentUserId;
    private bool _blurUnwatchedMedia;
    private ReviewPreferencesDto _reviewPreferences = new();

    public async Task RefreshAsync()
    {
        try
        {
            var localReviews = await ReviewService.GetMediaReviewsAsync(MediaId);
            IReadOnlyList<FederatedReviewDto> federatedReviews = [];
            try
            {
                federatedReviews = await ReviewService.GetFederatedMediaReviewsAsync(MediaId);
            }
            catch
            {
                // Federation/social peer failures must not hide local reviews or break the page.
            }

            _reviews.Clear();
            _reviews.AddRange(localReviews.Select(MediaReviewCardModel.FromLocal)
                .Concat(federatedReviews.Select(MediaReviewCardModel.FromFederated))
                .DistinctBy(r => r.Id));
            _reviews.Sort((a, b) => b.Created.CompareTo(a.Created));

            // Spoiler blur needs the current user, the review preferences and the watched state:
            // three calls a detail page does not need when there is no review to show.
            if (_reviews.Count > 0)
                await UpdateSpoilerStateAsync();
            else
                _blurUnwatchedMedia = false;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _reviews.Clear();
        }
        catch
        {
            // Keep whatever we already have; do not throw into the error boundary.
        }

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

        var watched = Watched ?? IsWatched((await MediaService.GetMediaAsync(MediaId))?.UserState);
        _blurUnwatchedMedia = !watched;
    }

    public static bool IsWatched(UserMediaStateDto? state) =>
        state is not null && (state.IsCompleted || state.PlayCount > 0);

    private bool IsReviewSpoilerBlurred(MediaReviewCardModel review) =>
        _blurUnwatchedMedia && review.UserId != _currentUserId;

    private Task OpenReviewAsync(MediaReviewCardModel review) =>
        MediaReviewDetailDialogHelper.OpenAsync(DialogService, DetailDialogL, review);
}
