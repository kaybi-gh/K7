using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Walks the app through the pages the custom AOT profile should cover (feeds, every detail page
/// type, browse, music, My space, settings), with a few D-pad moves on each, so recording the
/// profile needs nobody on the remote. Only the profiling build (-p:K7AotProfiling=true) sets
/// <see cref="Enabled"/>; other builds never start it. Ends with the K7-Startup marker
/// <c>AotProfileTour-done</c>, which tools/record-aot-profile.ps1 waits for.
/// </summary>
public static class AotProfileTour
{
    private static readonly TimeSpan PageSettle = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan DpadPause = TimeSpan.FromMilliseconds(350);
    // Android key codes, as the native remote relay passes them.
    private static readonly (string Key, int Code)[] DpadMoves =
    [
        ("ArrowRight", 22), ("ArrowRight", 22), ("ArrowDown", 20), ("ArrowRight", 22), ("ArrowUp", 19)
    ];
    private static readonly string[] StaticRoutes =
    [
        "/music/tracks", "/music/radio", "/music/stats",
        "/search?q=a",
        "/my-space", "/my-space/playlists", "/my-space/collections", "/my-space/stats",
        "/my-space/history", "/my-space/hit-parade", "/my-space/reviews",
        "/users", "/sessions",
        "/settings/account", "/settings/general", "/settings/home-layout", "/settings/navigation",
        "/settings/audio-player", "/settings/video-playback", "/settings/libraries",
        "/settings/social", "/settings/about",
        "/"
    ];

    private static int _started;

    public static bool Enabled { get; set; }

    /// <param name="invoke">The calling component's InvokeAsync, so navigation runs on the renderer.</param>
    public static void Start(
        IServiceProvider services,
        NavigationManager navigation,
        IJSRuntime js,
        Func<Func<Task>, Task> invoke)
    {
        if (!Enabled || Interlocked.Exchange(ref _started, 1) == 1)
            return;

        _ = RunAsync(services, navigation, js, invoke);
    }

    private static async Task RunAsync(
        IServiceProvider services,
        NavigationManager navigation,
        IJSRuntime js,
        Func<Func<Task>, Task> invoke)
    {
        ColdStartTrace.Write("AotProfileTour-start");
        await Task.Delay(PageSettle);

        var routes = await BuildRoutesAsync(services);
        foreach (var route in routes)
        {
            try
            {
                await invoke(() =>
                {
                    navigation.NavigateTo(route);
                    return Task.CompletedTask;
                });
                await Task.Delay(PageSettle);

                foreach (var (key, code) in DpadMoves)
                {
                    await js.InvokeVoidAsync("K7.dispatchTvShellDpad", key, code, false);
                    await Task.Delay(DpadPause);
                }
            }
            catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or TaskCanceledException)
            {
                // A page that fails here still left its code in the profile; keep touring.
            }
        }

        ColdStartTrace.Write("AotProfileTour-done");
    }

    private static async Task<List<string>> BuildRoutesAsync(IServiceProvider services)
    {
        var media = services.GetRequiredService<IMediaService>();
        var libraries = services.GetRequiredService<ILibraryService>();
        var routes = new List<string> { "/", "/explore" };

        try
        {
            foreach (var group in (await libraries.GetLibraryGroupsAsync()).Take(4))
            {
                routes.Add($"/explore?library-group={group.Id}");
                routes.Add($"/library-groups/{group.Id}");
            }

            foreach (var movieId in await FirstIdsAsync(media, MediaType.Movie, 2))
            {
                routes.Add($"/movies/{movieId}");
                if (await media.GetMediaAsync(movieId) is { PersonRoles: { Count: > 0 } roles }
                    && roles[0].Person?.Id is { } personId)
                {
                    routes.Add($"/persons/{personId}");
                }
            }

            foreach (var serieId in await FirstIdsAsync(media, MediaType.Serie, 2))
            {
                routes.Add($"/series/{serieId}");
                if (await media.GetMediaAsync(serieId) is not SerieDto serie)
                    continue;

                var season = serie.Seasons?.FirstOrDefault(s => s.EpisodeCount > 0);
                if (season is null)
                    continue;

                routes.Add($"/series/{serieId}/seasons/{season.SeasonNumber}");
                if (await media.GetMediaAsync(season.Id) is SerieSeasonDto { Episodes: { Count: > 0 } episodes })
                    routes.Add($"/series/{serieId}/seasons/{season.SeasonNumber}/episodes/{episodes[0].EpisodeNumber}");
            }

            foreach (var artistId in await FirstIdsAsync(media, MediaType.MusicArtist, 1))
                routes.Add($"/music/artists/{artistId}");

            foreach (var albumId in await FirstIdsAsync(media, MediaType.MusicAlbum, 1))
                routes.Add($"/music/albums/{albumId}");
        }
        catch (HttpRequestException)
        {
            // Partial library: tour what was found plus the static pages.
        }

        routes.AddRange(StaticRoutes);
        return routes;
    }

    private static async Task<IReadOnlyList<Guid>> FirstIdsAsync(IMediaService media, MediaType type, int count)
    {
        var page = await media.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [type],
            PageNumber = 1,
            PageSize = count
        });

        return page?.Items?.Select(i => i.Id).ToList() ?? [];
    }
}
