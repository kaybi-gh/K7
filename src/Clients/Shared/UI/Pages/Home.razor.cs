using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class Home : IDisposable
{
    [Inject] private IMediaService k7ServerService { get; set; } = default!;
    [Inject] private IK7ServerService apiClient { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    private bool isLoading { get; set; } = true;
    private bool isAuthenticated;
    private bool _canTrackProgress;
    private List<MediaPosterViewModel> continueWatchingMedias = [];
    private List<MediaPosterViewModel> recentlyAddedMedias = [];
    private List<MediaPosterViewModel> recentlyReleasedMedias = [];
    private List<MediaPosterViewModel> lastPlayedMedias = [];

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        isAuthenticated = authState.User.Identity?.IsAuthenticated == true;
        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);

        if (_canTrackProgress)
        {
            K7HubClient.ProgressUpdated += OnProgressUpdated;

            await InitializeContinueWatchingMedias();
        }

        await InitializeRecentlyAddedMedias();
        await InitializeRecentlyReleasedMedias();

        if (_canTrackProgress)
        {
            await InitializeLastPlayedMedias();
        }

        isLoading = false;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("SpatialNavigation.focusFirst", "[data-carousel-item]");
        }
    }

    private void OnProgressUpdated(Guid mediaId, double progressPercentage, bool isCompleted)
    {
        var id = mediaId.ToString();
        var changed = false;

        foreach (var list in new[] { continueWatchingMedias, recentlyAddedMedias, recentlyReleasedMedias, lastPlayedMedias })
        {
            for (var i = 0; i < list.Count; i++)
            {
                if (list[i].Id == id)
                {
                    list[i] = list[i] with { Progress = progressPercentage, Watched = isCompleted };
                    changed = true;
                }
            }
        }

        if (changed)
        {
            InvokeAsync(StateHasChanged);
        }
    }

    public void Dispose()
    {
        K7HubClient.ProgressUpdated -= OnProgressUpdated;
    }

    private async Task InitializeContinueWatchingMedias()
    {
        try
        {
            var mediasPage = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery()
            {
                ContinueWatching = true,
                OrderBy = [MediaOrderingOption.LastInteractedDesc],
                PageNumber = 1,
                PageSize = 20
            });

            if (mediasPage?.Items is not null)
            {
                foreach (var item in mediasPage.Items)
                {
                    if (MapToViewModel(item) is { } vm)
                        continueWatchingMedias.Add(vm);
                }
            }
        }
        catch { }
    }

    private async Task InitializeRecentlyAddedMedias()
    {
        try
        {
            var mediasPage = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery()
            {
                OrderBy = [MediaOrderingOption.CreatedDesc],
                PageNumber = 1,
                PageSize = 40
            });

            if (mediasPage?.Items is not null)
            {
                foreach (var item in mediasPage.Items)
                {
                    if (MapToViewModel(item) is { } vm)
                        recentlyAddedMedias.Add(vm);
                }
            }
        }
        catch { }
    }

    private async Task InitializeRecentlyReleasedMedias()
    {
        try
        {
            var mediasPage = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery()
            {
                OrderBy = [MediaOrderingOption.ReleaseDateDesc],
                PageNumber = 1,
                PageSize = 40
            });

            if (mediasPage?.Items is not null)
            {
                foreach (var item in mediasPage.Items)
                {
                    if (MapToViewModel(item) is { } vm)
                        recentlyReleasedMedias.Add(vm);
                }
            }
        }
        catch { }
    }

    private async Task InitializeLastPlayedMedias()
    {
        try
        {
            var mediasPage = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery()
            {
                OrderBy = [MediaOrderingOption.LastInteractedDesc],
                PageNumber = 1,
                PageSize = 40
            });

            if (mediasPage?.Items is not null)
            {
                foreach (var item in mediasPage.Items)
                {
                    if (MapToViewModel(item) is { } vm)
                        lastPlayedMedias.Add(vm);
                }
            }
        }
        catch { }
    }

    private MediaPosterViewModel? MapToViewModel(K7.Shared.Dtos.Entities.Medias.LiteMediaDto item)
    {
        var kind = item switch
        {
            K7.Shared.Dtos.Entities.Medias.LiteMusicAlbumDto => MediaPosterKind.Album,
            K7.Shared.Dtos.Entities.Medias.LiteMovieDto => MediaPosterKind.Movie,
            _ => (MediaPosterKind?)null
        };

        if (kind is null) return null;

        var userState = item.UserState;

        return new MediaPosterViewModel
        {
            Id = item.Id.ToString(),
            Kind = kind.Value,
            Title = item.Title,
            AdditionalInformations = item.ReleaseDate,
            PosterPictureHref = apiClient.GetAbsoluteUri(item.Pictures?.FirstOrDefault(x => x.Type == MetadataPictureType.Poster)?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri,
            Watched = userState?.IsCompleted ?? false,
            Progress = userState?.ProgressPercentage ?? 0
        };
    }
}
