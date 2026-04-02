using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
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
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private ISnackbar Snackbar { get; set; } = default!;
    [Inject] private IDialogService DialogService { get; set; } = default!;

    private bool isLoading { get; set; } = true;
    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _isAdmin;
    private readonly List<MediaCardViewModel> continueWatchingMedias = [];
    private readonly List<MediaCardViewModel> recentlyAddedMedias = [];
    private readonly List<MediaCardViewModel> recentlyReleasedMedias = [];
    private readonly List<MediaCardViewModel> lastPlayedMedias = [];
    private Timer? _mediaAddedDebounce;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var role = await FeatureAccess.GetRoleAsync();
        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
        _canExclude = role is not null and not K7.Server.Domain.Constants.Roles.Guest;
        _isAdmin = role == K7.Server.Domain.Constants.Roles.Administrator;

        K7HubClient.MediaAdded += OnMediaAdded;

        var tasks = new List<Task>();

        if (_canTrackProgress)
        {
            K7HubClient.ProgressUpdated += OnProgressUpdated;
            tasks.Add(LoadCarouselAsync(new GetMediasWithPaginationQuery
            {
                ContinueWatching = true,
                OrderBy = [MediaOrderingOption.LastInteractedDesc],
                PageNumber = 1,
                PageSize = 20
            }, continueWatchingMedias));
        }

        tasks.Add(LoadCarouselAsync(new GetMediasWithPaginationQuery
        {
            OrderBy = [MediaOrderingOption.CreatedDesc],
            PageNumber = 1,
            PageSize = 50
        }, recentlyAddedMedias, useParentTitle: true));

        tasks.Add(LoadCarouselAsync(new GetMediasWithPaginationQuery
        {
            OrderBy = [MediaOrderingOption.ReleaseDateDesc],
            PageNumber = 1,
            PageSize = 50
        }, recentlyReleasedMedias, useParentTitle: true));

        if (_canTrackProgress)
        {
            tasks.Add(LoadCarouselAsync(new GetMediasWithPaginationQuery
            {
                OrderBy = [MediaOrderingOption.LastInteractedDesc],
                PageNumber = 1,
                PageSize = 40
            }, lastPlayedMedias));
        }

        await Task.WhenAll(tasks);

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
        K7HubClient.MediaAdded -= OnMediaAdded;
        _mediaAddedDebounce?.Dispose();
    }

    private void OnMediaAdded(Guid mediaId, string? title, string mediaType)
    {
        _mediaAddedDebounce?.Dispose();
        _mediaAddedDebounce = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await RefreshRecentlyAddedAsync();
                StateHasChanged();
            });
        }, null, 10000, Timeout.Infinite);
    }

    private async Task RefreshRecentlyAddedAsync()
    {
        recentlyAddedMedias.Clear();
        await LoadCarouselAsync(new GetMediasWithPaginationQuery
        {
            OrderBy = [MediaOrderingOption.CreatedDesc],
            PageNumber = 1,
            PageSize = 100
        }, recentlyAddedMedias, useParentTitle: true);
    }

    private async Task LoadCarouselAsync(GetMediasWithPaginationQuery query, List<MediaCardViewModel> target, bool useParentTitle = false)
    {
        try
        {
            var mediasPage = await k7ServerService.GetLiteMediasAsync(query);
            if (mediasPage?.Items is null) return;

            if (!useParentTitle)
            {
                var seen = new HashSet<string>();
                foreach (var item in mediasPage.Items)
                {
                    if (item.ToCardViewModel(apiClient, false) is { } vm && seen.Add(vm.Id))
                        target.Add(vm);
                }
                return;
            }

            // Smart grouping: episodes are aggregated per serie, then resolved to Episode/Season/Serie cards
            var insertOrder = 0;
            var orderedCards = new List<(int Order, MediaCardViewModel Card)>();
            var serieInsertOrder = new Dictionary<string, int>();
            var serieEpisodes = new Dictionary<string, List<MediaCardViewModel>>();

            foreach (var item in mediasPage.Items)
            {
                if (item.ToCardViewModel(apiClient, useParentTitle: true) is not { } vm) continue;
                if (vm.Kind == MediaCardKind.Serie) continue;

                if (vm.Kind == MediaCardKind.Episode && vm.ParentId is not null)
                {
                    if (!serieInsertOrder.ContainsKey(vm.ParentId))
                    {
                        serieInsertOrder[vm.ParentId] = insertOrder++;
                        serieEpisodes[vm.ParentId] = [];
                    }
                    serieEpisodes[vm.ParentId].Add(vm);
                }
                else
                {
                    orderedCards.Add((insertOrder++, vm));
                }
            }

            foreach (var (serieId, episodes) in serieEpisodes)
            {
                var firstEp = episodes[0];
                var allWatched = episodes.All(e => e.Watched);
                MediaCardViewModel card = episodes.Count == 1
                    ? firstEp
                    : episodes.Select(e => e.SeasonNumber).Distinct().Count() == 1 && firstEp.SerieSeasonCount > 1
                        ? firstEp with { Kind = MediaCardKind.Season, GroupCount = episodes.Count, Watched = allWatched }
                        : firstEp with { Id = serieId, Kind = MediaCardKind.Serie, GroupCount = episodes.Count, Watched = allWatched };
                orderedCards.Add((serieInsertOrder[serieId], card));
            }

            target.AddRange(orderedCards.OrderBy(x => x.Order).Select(x => x.Card));
        }
        catch { }
    }

    private string GetHref(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Cover => $"/music/albums/{item.ParentId ?? item.Id}",
        MediaCardKind.Serie => $"/series/{item.Id}",
        MediaCardKind.Season => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}",
        MediaCardKind.Episode => $"/series/{item.ParentId ?? item.Id}/seasons/{item.SeasonNumber}#ep-{item.EpisodeNumber}",
        _ => $"/movies/{item.Id}"
    };

    private MediaCardVariant GetVariant(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Cover => MediaCardVariant.Cover,
        MediaCardKind.Episode => MediaCardVariant.Poster,
        _ => MediaCardVariant.Poster
    };

    private async Task ExcludeForSelf(MediaCardViewModel model)
    {
        try
        {
            var excluded = await UserAdminService.ToggleMediaExclusionAsync(Guid.Parse(model.Id));
            Snackbar.Add(excluded ? string.Format(S["Hidden"], model.Title) : string.Format(S["Unhidden"], model.Title), Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), Severity.Error);
        }
    }

    private async Task ExcludeForOthers(MediaCardViewModel model)
    {
        var parameters = new DialogParameters<ExcludeMediaForUsersDialog>
        {
            { x => x.MediaId, Guid.Parse(model.Id) },
            { x => x.MediaTitle, model.Title }
        };
        var options = new DialogOptions { MaxWidth = MaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ExcludeMediaForUsersDialog>(S["HideForUser"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(S["ExclusionsUpdated"], Severity.Success);
        }
    }
}
