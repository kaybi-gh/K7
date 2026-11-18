using System.Text.Json;
using K7.Clients.Shared.Interfaces;
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
/// Walks the app through what the custom AOT profile should cover, so recording the profile needs
/// nobody on the remote: every page (feeds, each detail page type, browse, music, My space,
/// settings) with a few D-pad moves, then a card menu, a movie and an episode playback with the
/// playback settings menu, and an album playback with the full screen music player. Menus and
/// playbacks are started by clicking the real buttons, so the page handlers end up in the profile.
/// Only the profiling build (-p:K7AotProfiling=true) sets <see cref="Enabled"/>; other builds
/// never start it. Ends with the K7-Startup marker <c>AotProfileTour-done</c>, which
/// tools/record-aot-profile.ps1 waits for.
/// </summary>
public static class AotProfileTour
{
    private const int PlaybackSettingsSections = 6;
    private static readonly TimeSpan PageSettle = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan DpadPause = TimeSpan.FromMilliseconds(350);
    private static readonly TimeSpan MenuPause = TimeSpan.FromMilliseconds(1200);
    private static readonly TimeSpan PlaybackStartTimeout = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan PlaybackHold = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan PlayerCloseTimeout = TimeSpan.FromSeconds(6);
    // Android key codes, as the native remote relay passes them.
    private static readonly (string Key, int Code)[] DpadMoves =
    [
        ("ArrowRight", 22), ("ArrowRight", 22), ("ArrowDown", 20), ("ArrowRight", 22), ("ArrowUp", 19)
    ];
    // One route per page component: aliases of the same page add nothing to the profile.
    private static readonly string[] StaticRoutes =
    [
        // No /music/tracks, /music/radio or /music/stats: nothing in the app links to those pages.
        "/playlists",
        // Search only runs from two characters on.
        "/search?q=an",
        "/my-space", "/my-space/playlists", "/my-space/collections", "/my-space/stats",
        "/my-space/history", "/my-space/hit-parade", "/my-space/reviews",
        "/my-space/shares", "/my-space/downloads", "/my-space/downloads/queue",
        "/users", "/sessions",
        "/settings/account", "/settings/device",
        "/settings/interface/appearance", "/settings/interface/theme-songs", "/settings/interface/media-pages",
        "/settings/interface/music-player", "/settings/interface/video-player",
        "/settings/interface/cards", "/settings/interface/home", "/settings/interface/navigation",
        "/settings/audio-player", "/settings/video-playback", "/settings/video-player", "/settings/track-selection",
        "/settings/libraries", "/settings/hidden", "/settings/history", "/settings/stats", "/settings/offline",
        "/settings/scrobbling", "/settings/external-apps", "/settings/shared-profiles", "/settings/syncplay",
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

        var plan = await BuildPlanAsync(services);
        var context = new TourContext(services, navigation, js, invoke);

        foreach (var route in plan.Routes)
        {
            await RunStepAsync(async () =>
            {
                await GoToAsync(context, route);
                foreach (var (key, code) in DpadMoves)
                {
                    await js.InvokeVoidAsync("K7.dispatchTvShellDpad", key, code, false);
                    await Task.Delay(DpadPause);
                }
            });
        }

        // The routes end on Home, where the first card menu is opened.
        ColdStartTrace.Write("AotProfileTour-menus");
        await RunStepAsync(() => CardMenuSceneAsync(context));

        ColdStartTrace.Write("AotProfileTour-video");
        if (plan.MovieRoute is not null)
            await RunStepAsync(() => VideoSceneAsync(context, plan.MovieRoute, ".movie-actions-play"));
        if (plan.EpisodeRoute is not null)
            await RunStepAsync(() => VideoSceneAsync(context, plan.EpisodeRoute, ".episode-actions-play"));

        ColdStartTrace.Write("AotProfileTour-audio");
        if (plan.AlbumRoute is not null)
            await RunStepAsync(() => AudioSceneAsync(context, plan.AlbumRoute));

        await RunStepAsync(() => GoToAsync(context, "/"));
        ColdStartTrace.Write("AotProfileTour-done");
    }

    private static async Task RunStepAsync(Func<Task> step)
    {
        try
        {
            await step();
        }
        catch (Exception ex) when (ex is JSException or JSDisconnectedException or InvalidOperationException or OperationCanceledException or HttpRequestException)
        {
            // A step that fails here still left its code in the profile; keep touring.
        }
    }

    private static async Task CardMenuSceneAsync(TourContext context)
    {
        if (!await ClickAsync(context.Js, ".media-card-menu-trigger"))
            return;

        await Task.Delay(MenuPause);
        await ClickAsync(context.Js, ".k7-backdrop");
        await Task.Delay(DpadPause);
    }

    private static async Task VideoSceneAsync(TourContext context, string route, string playSelector)
    {
        var player = context.Services.GetRequiredService<IPlayerService>();
        var js = context.Js;

        await GoToAsync(context, route);
        if (!await ClickAsync(js, playSelector))
            return;

        try
        {
            if (!await WaitUntilAsync(() => player.PlaybackState == PlaybackState.Playing, PlaybackStartTimeout))
                return;

            await Task.Delay(PlaybackHold);
            await OnRendererAsync(context, player.Pause);
            await Task.Delay(MenuPause);
            await OnRendererAsync(context, player.Play);
            await Task.Delay(MenuPause);
            await OnRendererAsync(context, () => player.Seek(player.CurrentTime + 60));
            await Task.Delay(PlaybackHold);

            // Open the settings menu once per section: selecting one replaces the section list.
            for (var section = 0; section < PlaybackSettingsSections; section++)
            {
                if (!await ClickAsync(js, ".playback-settings-activator button"))
                    break;

                await Task.Delay(MenuPause);
                var selected = await ClickAsync(js, ".playback-settings-nav-item", section);
                await Task.Delay(MenuPause);
                await ClickAsync(js, ".playback-settings-close");
                await Task.Delay(DpadPause);
                if (!selected)
                    break;
            }
        }
        finally
        {
            await ClickAsync(js, ".video-player-close-btn");
            if (!await WaitUntilAsync(() => !player.IsVisible, PlayerCloseTimeout))
            {
                await context.Invoke(async () =>
                {
                    player.Stop();
                    await player.HideAsync();
                });
            }

            await Task.Delay(PageSettle);
        }
    }

    private static async Task AudioSceneAsync(TourContext context, string route)
    {
        var audio = context.Services.GetRequiredService<IAudioPlayerService>();
        var js = context.Js;

        await GoToAsync(context, route);
        if (!await ClickAsync(js, ".album-actions-primary button"))
            return;

        try
        {
            if (!await WaitUntilAsync(() => audio.PlaybackState == PlaybackState.Playing, PlaybackStartTimeout))
                return;

            await Task.Delay(PlaybackHold);
            if (!audio.IsFullScreenVisible)
                await OnRendererAsync(context, audio.ToggleFullScreen);
            await Task.Delay(PageSettle);

            foreach (var view in (string[])["Lyrics", "Queue", "Queue"])
            {
                await ClickAsync(js, $".fullscreen-player [aria-label=\"{view}\"]");
                await Task.Delay(MenuPause);
            }

            if (await ClickAsync(js, ".fullscreen-player .k7-menu-activator-inner"))
            {
                await Task.Delay(MenuPause);
                await ClickAsync(js, ".k7-backdrop");
                await Task.Delay(DpadPause);
            }

            await context.Invoke(() => audio.NextAsync());
            await Task.Delay(PlaybackHold);
            await OnRendererAsync(context, () => audio.Seek(30));
            await Task.Delay(MenuPause);

            // Even number of shuffle toggles and a full repeat cycle leave the modes as they were.
            await OnRendererAsync(context, audio.ToggleShuffle);
            await OnRendererAsync(context, audio.ToggleShuffle);
            for (var mode = 0; mode < Enum.GetValues<RepeatMode>().Length; mode++)
                await OnRendererAsync(context, audio.CycleRepeatMode);

            await OnRendererAsync(context, audio.Pause);
            await Task.Delay(MenuPause);
            await OnRendererAsync(context, audio.Play);
            await Task.Delay(MenuPause);

            // Back to the mini player before stopping.
            if (audio.IsFullScreenVisible)
                await OnRendererAsync(context, audio.ToggleFullScreen);
            await Task.Delay(PageSettle);
        }
        finally
        {
            await context.Invoke(async () =>
            {
                audio.Stop();
                await audio.HideAsync();
            });
            await Task.Delay(MenuPause);
        }
    }

    private static async Task GoToAsync(TourContext context, string route)
    {
        await OnRendererAsync(context, () => context.Navigation.NavigateTo(route));
        await Task.Delay(PageSettle);
    }

    private static Task OnRendererAsync(TourContext context, Action action) =>
        context.Invoke(() =>
        {
            action();
            return Task.CompletedTask;
        });

    /// <summary>Clicks the index-th element matching the selector; false when there is none.</summary>
    private static async Task<bool> ClickAsync(IJSRuntime js, string selector, int index = 0)
    {
        var script = "(function(){var e=document.querySelectorAll(" + JsonSerializer.Serialize(selector) + ")[" + index
            + "];if(!e||e.disabled)return false;e.click();return true;})()";
        return await js.InvokeAsync<bool>("eval", script);
    }

    private static async Task<bool> WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            if (condition())
                return true;

            await Task.Delay(DpadPause);
        }

        return condition();
    }

    private static async Task<TourPlan> BuildPlanAsync(IServiceProvider services)
    {
        var media = services.GetRequiredService<IMediaService>();
        var libraries = services.GetRequiredService<ILibraryService>();
        var plan = new TourPlan();
        var routes = plan.Routes;
        routes.Add("/");
        routes.Add("/explore");

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
                plan.MovieRoute ??= $"/movies/{movieId}";
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
                {
                    var episodeRoute = $"/series/{serieId}/seasons/{season.SeasonNumber}/episodes/{episodes[0].EpisodeNumber}";
                    routes.Add(episodeRoute);
                    plan.EpisodeRoute ??= episodeRoute;
                }
            }

            foreach (var artistId in await FirstIdsAsync(media, MediaType.MusicArtist, 1))
                routes.Add($"/music/artists/{artistId}");

            foreach (var albumId in await FirstIdsAsync(media, MediaType.MusicAlbum, 1))
            {
                routes.Add($"/music/albums/{albumId}");
                plan.AlbumRoute ??= $"/music/albums/{albumId}";
            }
        }
        catch (HttpRequestException)
        {
            // Partial library: tour what was found plus the static pages.
        }

        routes.AddRange(StaticRoutes);
        return plan;
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

    private sealed record TourContext(
        IServiceProvider Services,
        NavigationManager Navigation,
        IJSRuntime Js,
        Func<Func<Task>, Task> Invoke);

    private sealed class TourPlan
    {
        public List<string> Routes { get; } = [];

        public string? MovieRoute { get; set; }

        public string? EpisodeRoute { get; set; }

        public string? AlbumRoute { get; set; }
    }
}
