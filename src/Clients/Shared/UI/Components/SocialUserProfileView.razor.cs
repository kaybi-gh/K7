using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class SocialUserProfileView
{
    [Inject] private IReviewService ReviewService { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;

    [Parameter] public Func<CancellationToken, Task<SocialUserProfileDto?>> LoadProfileAsync { get; set; } = default!;
    [Parameter] public bool CanCopyPlaylist { get; set; }
    [Parameter] public Func<Guid, CancellationToken, Task<Guid>>? CopyPlaylistAsync { get; set; }

    private SocialUserProfileDto? _profile;
    private bool _loading = true;
    private bool _canCopyPlaylist;
    private Carousel? _reviewsCarousel;
    private List<SocialUserReviewViewDto> _recentReviews = [];
    private List<SocialUserPlaybackViewDto> _recentPlayback = [];
    private bool _showPlaybackSection;
    private Guid? _avatarUserId;
    private bool _isOwnProfile;
    private readonly HashSet<Guid> _blurredReviewIds = [];
    private ReviewPreferencesDto _reviewPreferences = new();

    private const int MaxPlaybackItems = 10;
    private const float PlaybackRowHeight = 48;

    private string PlaybackTableHeight =>
        FormattableString.Invariant($"calc(var(--k7-data-table-row-height) * {_recentPlayback.Count + 1})");

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
        _canCopyPlaylist = CanCopyPlaylist && CopyPlaylistAsync is not null;
        var me = await UserAdminService.GetCurrentUserAsync();
        _profile = await LoadProfileAsync(CancellationToken.None);
        _isOwnProfile = me?.Id is Guid viewerId && _profile?.Identity.LocalUserId == viewerId;
        _avatarUserId = _profile?.Identity.LocalUserId ?? _profile?.Identity.OriginUserId;
        _recentPlayback = _profile?.RecentPlayback.Take(MaxPlaybackItems).ToList() ?? [];
        _showPlaybackSection = _profile?.VisibleSections.PlaybackHistory ?? false;
        _recentReviews = _profile?.RecentReviews.ToList() ?? [];

        await UpdateSpoilerStateAsync();

        if (_reviewsCarousel is not null)
            await _reviewsCarousel.NotifyItemsChangedAsync();

        _loading = false;
    }

    private async Task UpdateSpoilerStateAsync()
    {
        _blurredReviewIds.Clear();

        if (_recentReviews.Count == 0 || _isOwnProfile)
            return;

        _reviewPreferences = await ReviewService.GetReviewPreferencesAsync();
        if (!_reviewPreferences.BlurReviewsForUnwatchedMedia)
            return;

        var mediaIds = _recentReviews
            .Select(r => r.Media.LocalMediaId)
            .Where(id => id is not null)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var watchedTasks = mediaIds.Select(async mediaId =>
        {
            var media = await MediaService.GetMediaAsync(mediaId);
            var state = media?.UserState;
            return (mediaId, Watched: state is not null && (state.IsCompleted || state.PlayCount > 0));
        });

        var watchedResults = await Task.WhenAll(watchedTasks);
        var watchedByMediaId = watchedResults.ToDictionary(r => r.mediaId, r => r.Watched);

        foreach (var review in _recentReviews)
        {
            if (review.Media.LocalMediaId is not Guid mediaId)
                continue;

            if (!watchedByMediaId.GetValueOrDefault(mediaId))
                _blurredReviewIds.Add(review.Id);
        }
    }

    private bool IsReviewSpoilerBlurred(SocialUserReviewViewDto review) =>
        !_isOwnProfile && _reviewPreferences.BlurReviewsForUnwatchedMedia && _blurredReviewIds.Contains(review.Id);

    private void OnPlaybackRowClick(SocialUserPlaybackViewDto entry)
    {
        var href = SocialUserNavigation.GetPlaybackHref(entry);
        if (href is not null)
            NavigationManager.NavigateTo(href);
    }

    private string FormatMediaType(MediaType? mediaType) =>
        mediaType is MediaType type ? MediaTypeLabelHelper.Format(type, S) : "-";

    private Task OpenReviewAsync(SocialUserReviewViewDto review)
    {
        if (_profile is null)
            return Task.CompletedTask;

        var cardModel = MediaReviewCardModel.FromProfileReview(review, _profile.Identity);
        return MediaReviewDetailDialogHelper.OpenAsync(DialogService, DetailDialogL, cardModel);
    }

    private async Task OnCopyPlaylistAsync(Guid playlistId)
    {
        if (CopyPlaylistAsync is null)
            return;

        try
        {
            var id = await CopyPlaylistAsync(playlistId, CancellationToken.None);
            Snackbar.Add(L["PlaylistCopied"], K7Severity.Success);
            NavigationManager.NavigateTo($"/playlists/{id}");
        }
        catch
        {
            Snackbar.Add(L["PlaylistCopyFailed"], K7Severity.Error);
        }
    }

    private MediaCardViewModel GetCollectionCardModel(SocialUserCollectionCardDto collection) =>
        collection.ToCardViewModel(GetCollectionSubtitle(collection));

    private MediaCardViewModel GetPlaylistCardModel(SocialUserPlaylistCardDto playlist) =>
        playlist.ToCardViewModel(GetPlaylistSubtitle(playlist));

    private string GetCollectionHref(SocialUserCollectionCardDto collection) =>
        $"/collections/{collection.Id}";

    private string GetPlaylistHref(SocialUserPlaylistCardDto playlist) =>
        playlist.IsSmart ? $"/smart-playlists/{playlist.Id}" : $"/playlists/{playlist.Id}";

    private string GetCollectionSubtitle(SocialUserCollectionCardDto collection) =>
        $"{collection.ItemCount} {L["Items"]}";

    private string GetPlaylistSubtitle(SocialUserPlaylistCardDto playlist) =>
        $"{playlist.ItemCount} {L["Items"]}";
}
