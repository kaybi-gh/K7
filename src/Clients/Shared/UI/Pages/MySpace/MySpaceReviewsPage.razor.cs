using K7.Clients.Shared.Enums;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Clients.Shared.UI.Extensions;
using K7.Clients.Shared.UI.Helpers;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Federation.Social;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.MySpace;

public partial class MySpaceReviewsPage
{
    private const string FilterStorageKey = "my-space-reviews";

    [Inject] private IReviewService ReviewService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IPageFilterStorage PageFilterStorage { get; set; } = default!;

    private BrowseView<SocialUserReviewViewDto>? _browseView;
    private bool _loading = true;
    private List<SocialUserReviewViewDto> _allReviews = [];
    private List<SocialUserReviewViewDto> _filteredReviews = [];
    private MediaType? _mediaTypeFilter;
    private ReviewOrderingOption _selectedSort = ReviewOrderingOption.CreatedDesc;
    private List<ButtonGroupOption<MediaType?>> _mediaTypeOptions = [];

    protected override async Task OnInitializedAsync()
    {
        await LoadPersistedFiltersAsync();
        await LoadReviewsAsync();
    }

    private async Task LoadReviewsAsync()
    {
        _loading = true;
        _allReviews = (await ReviewService.GetMyMediaReviewsAsync()).ToList();
        BuildMediaTypeOptions();
        ApplyFilters();
        _loading = false;

        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

    private void BuildMediaTypeOptions()
    {
        var types = _allReviews
            .Select(r => r.Media.Media.Type)
            .Distinct()
            .ToList();

        _mediaTypeOptions = [new(null, L["All"])];

        if (types.Any(IsMovieType))
            _mediaTypeOptions.Add(new(MediaType.Movie, L["FilterMovies"]));

        if (types.Any(IsTvType))
            _mediaTypeOptions.Add(new(MediaType.SerieEpisode, L["TVShows"]));

        if (types.Any(IsMusicType))
            _mediaTypeOptions.Add(new(MediaType.MusicTrack, L["Music"]));

        if (_mediaTypeFilter is MediaType filter && !_mediaTypeOptions.Any(o => o.Value == filter))
            _mediaTypeFilter = null;
    }

    private void ApplyFilters()
    {
        var filtered = _allReviews.Where(MatchesMediaTypeFilter);
        _filteredReviews = MySpaceReviewsBrowseSort.Apply(filtered, _selectedSort).ToList();
    }

    private bool MatchesMediaTypeFilter(SocialUserReviewViewDto review)
    {
        if (_mediaTypeFilter is not MediaType filter)
            return true;

        var type = review.Media.Media.Type;
        return filter switch
        {
            MediaType.Movie => IsMovieType(type),
            MediaType.SerieEpisode => IsTvType(type),
            MediaType.MusicTrack => IsMusicType(type),
            _ => type == filter
        };
    }

    private static bool IsMovieType(MediaType type) => type == MediaType.Movie;

    private static bool IsTvType(MediaType type) =>
        type is MediaType.Serie or MediaType.SerieSeason or MediaType.SerieEpisode;

    private static bool IsMusicType(MediaType type) =>
        type is MediaType.MusicTrack or MediaType.MusicAlbum or MediaType.MusicArtist;

    private async Task OnMediaTypeFilterChanged(MediaType? value)
    {
        _mediaTypeFilter = value;
        ApplyFilters();
        await PersistFiltersAsync();

        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

    private async Task OnSortChanged(ReviewOrderingOption value)
    {
        _selectedSort = value;
        ApplyFilters();
        await PersistFiltersAsync();

        if (_browseView is not null)
            await _browseView.RefreshAsync();
    }

    private string GetSortLabel(ReviewOrderingOption option) =>
        MySpaceReviewsBrowseSort.GetLabel(option, L);

    private async Task EditReviewAsync(SocialUserReviewViewDto review)
    {
        if (review.Media.LocalMediaId is not Guid mediaId)
            return;

        var mediaTitle = review.Media.Media.Title;
        var changed = await MediaReviewDialogHelper.OpenAsync(
            DialogService,
            ReviewDialogL,
            mediaId,
            mediaTitle);

        if (changed)
            await LoadReviewsAsync();
    }

    private async Task DeleteReviewAsync(SocialUserReviewViewDto review)
    {
        if (review.Media.LocalMediaId is not Guid mediaId)
            return;

        var mediaTitle = review.Media.Media.Title ?? S["Untitled"];
        var confirmed = await DialogService.ShowMessageBoxAsync(
            L["DeleteConfirmTitle"],
            string.Format(L["DeleteConfirmMessage"], mediaTitle),
            yesText: S["Delete"],
            cancelText: S["Cancel"]);

        if (confirmed != true)
            return;

        try
        {
            await ReviewService.DeleteMediaReviewAsync(mediaId);
            Snackbar.Add(ReviewDialogL["Deleted"], K7Severity.Success);
            await LoadReviewsAsync();
        }
        catch
        {
            Snackbar.Add(ReviewDialogL["DeleteError"], K7Severity.Error);
        }
    }

    private async Task LoadPersistedFiltersAsync()
    {
        var state = await PageFilterStorage.LoadAsync<MySpaceReviewsFilterState>(FilterStorageKey);
        if (state is null)
            return;

        if (state.MediaType is not null && Enum.IsDefined(typeof(MediaType), state.MediaType.Value))
            _mediaTypeFilter = state.MediaType;

        if (Enum.IsDefined(typeof(ReviewOrderingOption), state.Sort))
            _selectedSort = state.Sort;
    }

    private Task PersistFiltersAsync() =>
        PageFilterStorage.SaveAsync(FilterStorageKey, new MySpaceReviewsFilterState
        {
            MediaType = _mediaTypeFilter,
            Sort = _selectedSort
        });

    private sealed class MySpaceReviewsFilterState
    {
        public MediaType? MediaType { get; set; }
        public ReviewOrderingOption Sort { get; set; } = ReviewOrderingOption.CreatedDesc;
    }
}
