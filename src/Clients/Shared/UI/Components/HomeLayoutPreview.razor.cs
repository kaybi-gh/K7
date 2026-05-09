using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components;

public partial class HomeLayoutPreview
{
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IStringLocalizer<SharedResource> S { get; set; } = default!;

    [Parameter] public IReadOnlyList<HomeRowEditModel> Rows { get; set; } = [];

    private List<HomeRowConfigDto> _rows = [];
    private List<(string Title, List<MediaCardViewModel> Items)> _loaded = [];
    private bool _loading;

    public async Task RefreshAsync()
    {
        _rows = Rows
            .Where(r => r.IsVisible)
            .OrderBy(r => r.Order)
            .Select(r => r.ToDto())
            .ToList();

        _loaded = _rows.Select(r => (r.Title, new List<MediaCardViewModel>())).ToList();
        _loading = true;

        var tasks = _loaded
            .Select((entry, i) => LoadRowAsync(_rows[i], entry.Items))
            .ToList();

        await Task.WhenAll(tasks);
        _loading = false;
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

        try
        {
            var mediasPage = await MediaService.GetLiteMediasAsync(query);
            if (mediasPage?.Items is null) return;

            var insertOrder = 0;
            var orderedCards = new List<(int Order, MediaCardViewModel Card)>();
            var serieInsertOrder = new Dictionary<string, int>();
            var serieEpisodes = new Dictionary<string, List<MediaCardViewModel>>();
            var albumInsertOrder = new Dictionary<string, int>();
            var albumTracks = new Dictionary<string, List<MediaCardViewModel>>();

            foreach (var item in mediasPage.Items)
            {
                if (item.ToCardViewModel(ApiClient, n => string.Format(S["SeasonNumber"], n), useParentTitle: true) is not { } vm) continue;
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
                var card = episodes.Count == 1
                    ? firstEp
                    : episodes.Select(e => e.SeasonNumber).Distinct().Count() == 1 && firstEp.SerieSeasonCount > 1
                        ? firstEp with { Kind = MediaCardKind.Season, GroupCount = episodes.Count, Watched = allWatched }
                        : firstEp with { Id = serieId, Kind = MediaCardKind.Serie, GroupCount = episodes.Count, Watched = allWatched };
                orderedCards.Add((serieInsertOrder[serieId], card));
            }

            foreach (var (albumId, tracks) in albumTracks)
            {
                if (orderedCards.Any(c => c.Card.Id == albumId && c.Card.Kind == MediaCardKind.Cover))
                    continue;
                var firstTrack = tracks[0];
                orderedCards.Add((albumInsertOrder[albumId], firstTrack with { Id = albumId }));
            }

            target.AddRange(orderedCards.OrderBy(x => x.Order).Select(x => x.Card));
        }
        catch { }
    }

    private static MediaCardVariant GetVariant(MediaCardViewModel item) => item.Kind == MediaCardKind.Cover
        ? MediaCardVariant.Cover
        : MediaCardVariant.Poster;
}
