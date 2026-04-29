using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace K7.Clients.Shared.UI.Pages;

public partial class Home : IDisposable
{
    [Inject] private IMediaService k7ServerService { get; set; } = default!;
    [Inject] private IK7ServerService apiClient { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;
    [Inject] private AuthenticationStateProvider AuthenticationStateProvider { get; set; } = default!;
    [Inject] private K7.Clients.Shared.Services.K7HubClient K7HubClient { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private ISpatialNavService SpatialNav { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    private bool isLoading { get; set; } = true;
    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _isAdmin;
    private List<(HomeRowConfigDto Config, List<MediaCardViewModel> Items)> _rows = [];
    private List<MediaCardViewModel> _heroItems = [];
    private int _activeHeroIndex;
    private Timer? _mediaAddedDebounce;

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var role = await FeatureAccess.GetRoleAsync();
        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
        _canExclude = role is not null and not K7.Server.Domain.Constants.Roles.Guest;
        _isAdmin = role == K7.Server.Domain.Constants.Roles.Administrator;

        K7HubClient.MediaAdded += OnMediaAdded;

        HomeLayoutDto layout;
        try
        {
            layout = await PreferencesService.GetHomeLayoutAsync();
        }
        catch
        {
            layout = new HomeLayoutDto { Rows = [] };
        }

        _rows = layout.Rows
            .Where(r => r.IsVisible)
            .OrderBy(r => r.Order)
            .Select(r => (r, new List<MediaCardViewModel>()))
            .ToList();

        if (_canTrackProgress)
            K7HubClient.ProgressUpdated += OnProgressUpdated;

        var tasks = _rows
            .Where(r => !r.Config.ContinueWatching || _canTrackProgress)
            .Select(r => LoadRowAsync(r.Config, r.Items))
            .ToList();

        await Task.WhenAll(tasks);

        isLoading = false;
        var heroRows = _rows.Where(r => r.Config.DisplayType == HomeRowDisplayType.Hero).ToList();
        _heroItems = heroRows.Count > 0
            ? heroRows.SelectMany(r => r.Items).Take(5).ToList()
            : _rows
                .Where(r => !r.Config.ContinueWatching)
                .SelectMany(r => r.Items)
                .Where(x => !string.IsNullOrEmpty(x.BackdropUrl))
                .Take(5)
                .ToList();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try
            {
                await SpatialNav.FocusFirstAsync("[data-carousel-item] a, [data-carousel-item] button");
            }
            catch (InvalidOperationException) { }
        }
    }

    private void OnProgressUpdated(Guid mediaId, double progressPercentage, bool isCompleted)
    {
        var id = mediaId.ToString();
        var changed = false;

        foreach (var (_, items) in _rows)
        {
            for (var i = 0; i < items.Count; i++)
            {
                if (items[i].Id == id)
                {
                    items[i] = items[i] with { Progress = progressPercentage, Watched = isCompleted };
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

    private void OnHeroActiveIndexChanged(int index)
    {
        _activeHeroIndex = index;
    }

    private void OnMediaAdded(Guid mediaId, string? title, string mediaType)
    {
        _mediaAddedDebounce?.Dispose();
        _mediaAddedDebounce = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                await RefreshNonContinueWatchingRowsAsync();
                StateHasChanged();
            });
        }, null, 10000, Timeout.Infinite);
    }

    private async Task RefreshNonContinueWatchingRowsAsync()
    {
        var tasks = _rows
            .Where(r => !r.Config.ContinueWatching)
            .Select(async r =>
            {
                r.Items.Clear();
                await LoadRowAsync(r.Config, r.Items);
            });

        await Task.WhenAll(tasks);
    }

    private async Task LoadRowAsync(HomeRowConfigDto config, List<MediaCardViewModel> target)
    {
        var query = new GetMediasWithPaginationQuery
        {
            ContinueWatching = config.ContinueWatching ? true : null,
            LibraryIds = config.LibraryIds?.ToArray(),
            MediaTypes = config.MediaTypes is { Count: > 0 } mt ? mt.ToHashSet() : null,
            OrderBy = config.OrderBy is { Count: > 0 } ob ? ob.ToHashSet() : null,
            PageNumber = 1,
            PageSize = config.PageSize
        };

        await LoadCarouselAsync(query, target, useParentTitle: true);
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

            // Smart grouping: episodes are aggregated per serie, tracks are aggregated per album
            var insertOrder = 0;
            var orderedCards = new List<(int Order, MediaCardViewModel Card)>();
            var serieInsertOrder = new Dictionary<string, int>();
            var serieEpisodes = new Dictionary<string, List<MediaCardViewModel>>();
            var albumInsertOrder = new Dictionary<string, int>();
            var albumTracks = new Dictionary<string, List<MediaCardViewModel>>();

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
                else if (vm.Kind == MediaCardKind.Cover && vm.ParentId is not null)
                {
                    // Track card showing album info — group by album
                    if (!albumInsertOrder.ContainsKey(vm.ParentId))
                    {
                        albumInsertOrder[vm.ParentId] = insertOrder++;
                        albumTracks[vm.ParentId] = [];
                    }
                    albumTracks[vm.ParentId].Add(vm);
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

            foreach (var (albumId, tracks) in albumTracks)
            {
                // Skip if a direct album card (LiteMusicAlbumDto) was already added
                if (orderedCards.Any(c => c.Card.Id == albumId && c.Card.Kind == MediaCardKind.Cover))
                    continue;
                var firstTrack = tracks[0];
                orderedCards.Add((albumInsertOrder[albumId], firstTrack with { Id = albumId }));
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
            Snackbar.Add(excluded ? string.Format(S["Hidden"], model.Title) : string.Format(S["Unhidden"], model.Title), K7Severity.Success);

            if (excluded)
            {
                foreach (var (_, items) in _rows)
                    items.RemoveAll(x => x.Id == model.Id || x.ParentId == model.Id);
                _heroItems.RemoveAll(x => x.Id == model.Id || x.ParentId == model.Id);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task ExcludeForOthers(MediaCardViewModel model)
    {
        var parameters = new K7DialogParameters<ExcludeMediaForUsersDialog>
        {
            { x => x.MediaId, Guid.Parse(model.Id) },
            { x => x.MediaTitle, model.Title }
        };
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<ExcludeMediaForUsersDialog>(S["HideForUser"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            Snackbar.Add(S["ExclusionsUpdated"], K7Severity.Success);
        }
    }
}
