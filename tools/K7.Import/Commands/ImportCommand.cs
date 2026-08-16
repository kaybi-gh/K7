using System.CommandLine;
using K7.Import.Auth;
using K7.Import.Clients;
using K7.Import.Matching;
using K7.Import.Models;
using K7.Import.Sources;
using K7.Import.Sources.Jellyfin;
using K7.Import.Sources.Plex;
using K7.Import.Sources.Spotify;
using K7.Import.Sources.Tautulli;
using K7.Import.Sources.Tracearr;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using Spectre.Console;

namespace K7.Import.Commands;

public sealed class ImportCommand
{
    public static RootCommand CreateRoot()
    {
        var sourceOption = new Option<string>("--source", "-s") { Description = "Source type: plex, jellyfin, tautulli, tracearr, or spotify", Required = true };
        var sourceUrlOption = new Option<string>("--source-url") { Description = "Source server URL (not required for spotify)" };
        var sourceApiKeyOption = new Option<string>("--source-api-key") { Description = "Source server API key / token (not required for spotify with --spotify-data-dir)" };
        var k7UrlOption = new Option<string>("--k7-url") { Description = "K7 server URL", Required = true };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Preview matching and import counts without writing" };
        var includeOption = new Option<string[]>("--include") { Description = "Data types to import: history, ratings, playlists (default: all)", AllowMultipleArgumentsPerToken = true };
        var spotifyDataDirOption = new Option<string>("--spotify-data-dir") { Description = "Path to Spotify export folder (streaming history and/or Playlist*.json account data)" };
        var userMappingOption = new Option<string[]>("--user-mapping") { Description = "Map source user to K7 user (format: sourceUser:k7User)", AllowMultipleArgumentsPerToken = true };
        var usersOption = new Option<string[]>("--users") { Description = "Only import these source users (remote id or remote name, case-insensitive). Repeatable. Off by default (all source users)", AllowMultipleArgumentsPerToken = true };
        var autoMapUsersOption = new Option<bool>("--auto-map-users") { Description = "Auto-map source users to K7 users with the same username (case-insensitive). Off by default; unmapped users get temp plex-/jellyfin-/... accounts" };
        var includeDynamicPlaylistsOption = new Option<bool>("--include-dynamic-playlists") { Description = "Import Plex smart/dynamic playlists as static snapshots (skipped by default; prefer recreating as K7 dynamic playlists)" };
        var onlyMatchExistingOption = new Option<bool>("--only-match-existing") { Description = "Only import data for media that already exists in K7 - skip virtual media creation for unmatched items" };
        var fetchMetadataOption = new Option<bool>("--fetch-metadata") { Description = "Fetch rich metadata (posters, descriptions, etc.) for newly created media" };
        var playcountModeOption = new Option<string>("--playcount-mode") { Description = "PlayCount merge strategy: additive or max (default: additive)", DefaultValueFactory = _ => "additive" };
        var ratingModeOption = new Option<string>("--rating-mode") { Description = "Rating conflict strategy: keep or overwrite (default: keep)", DefaultValueFactory = _ => "keep" };
        var progressModeOption = new Option<string>("--progress-mode") { Description = "Progress conflict strategy: recent or overwrite (default: recent)", DefaultValueFactory = _ => "recent" };
        var pathMapOption = new Option<string[]>("--path-map")
        {
            Description = "Map Plex file path prefix to K7 indexed path prefix (format: plexPrefix:k7Prefix or plexPrefix=>k7Prefix). Repeatable.",
            AllowMultipleArgumentsPerToken = true
        };
        var reportOption = new Option<string>("--report", "-o")
        {
            Description = "Write the full import/dry-run report to a UTF-8 text file (recommended for large imports)"
        };
        var tracearrServerOption = new Option<string>("--tracearr-server")
        {
            Description = "Tracearr only: filter history to one media server (plex|jellyfin|emby, or a Tracearr server UUID / UUID prefix)"
        };
        var plexDbOption = new Option<string>("--plex-db")
        {
            Description = "Plex only: path to a copy of com.plexapp.plugins.library.db (Home-user ratings by account_id; required when PMS serves admin ratings for local profiles)"
        };

        var command = new RootCommand("K7 Import Tool - Import media data from Plex, Jellyfin, Tautulli, Tracearr, or Spotify into K7");
        command.Add(sourceOption);
        command.Add(sourceUrlOption);
        command.Add(sourceApiKeyOption);
        command.Add(k7UrlOption);
        command.Add(dryRunOption);
        command.Add(includeOption);
        command.Add(spotifyDataDirOption);
        command.Add(userMappingOption);
        command.Add(usersOption);
        command.Add(autoMapUsersOption);
        command.Add(includeDynamicPlaylistsOption);
        command.Add(onlyMatchExistingOption);
        command.Add(fetchMetadataOption);
        command.Add(playcountModeOption);
        command.Add(ratingModeOption);
        command.Add(progressModeOption);
        command.Add(pathMapOption);
        command.Add(reportOption);
        command.Add(tracearrServerOption);
        command.Add(plexDbOption);

        command.SetAction(async (parseResult, cancellationToken) =>
        {
            var source = parseResult.GetRequiredValue(sourceOption);
            var sourceUrl = parseResult.GetValue(sourceUrlOption) ?? "";
            var sourceApiKey = parseResult.GetValue(sourceApiKeyOption) ?? "";
            var k7Url = parseResult.GetRequiredValue(k7UrlOption);
            var dryRun = parseResult.GetValue(dryRunOption);
            var include = parseResult.GetValue(includeOption);
            var spotifyDataDir = parseResult.GetValue(spotifyDataDirOption);
            var userMapping = parseResult.GetValue(userMappingOption) ?? [];
            var userFilters = parseResult.GetValue(usersOption) ?? [];
            var autoMapUsers = parseResult.GetValue(autoMapUsersOption);
            var includeDynamicPlaylists = parseResult.GetValue(includeDynamicPlaylistsOption);
            var scope = ParseIncludeScope(include);
            var createMissing = !parseResult.GetValue(onlyMatchExistingOption);
            var fetchMetadata = parseResult.GetValue(fetchMetadataOption);
            var pathMaps = parseResult.GetValue(pathMapOption) ?? [];
            var reportPath = parseResult.GetValue(reportOption);
            var tracearrServer = parseResult.GetValue(tracearrServerOption);
            var plexDbPath = parseResult.GetValue(plexDbOption);

            var strategy = new MergeStrategy
            {
                PlayCount = parseResult.GetValue(playcountModeOption) == "max"
                    ? PlayCountMergeMode.Max : PlayCountMergeMode.Additive,
                Rating = parseResult.GetValue(ratingModeOption) == "overwrite"
                    ? RatingConflictMode.Overwrite : RatingConflictMode.KeepExisting,
                Progress = parseResult.GetValue(progressModeOption) == "overwrite"
                    ? ProgressConflictMode.AlwaysOverwrite : ProgressConflictMode.MostRecent
            };

            await RunAsync(source, sourceUrl, sourceApiKey, k7Url, dryRun, scope, spotifyDataDir, userMapping, userFilters, autoMapUsers, includeDynamicPlaylists, createMissing, fetchMetadata, strategy, pathMaps, reportPath, tracearrServer, plexDbPath, cancellationToken);
        });

        return command;
    }

    private static async Task RunAsync(
        string source,
        string sourceUrl,
        string sourceApiKey,
        string k7Url,
        bool dryRun,
        ImportScope scope,
        string? spotifyDataDir,
        string[] userMappings,
        string[] userFilters,
        bool autoMapUsers,
        bool includeDynamicPlaylists,
        bool createMissing,
        bool fetchMetadata,
        MergeStrategy strategy,
        string[] pathMapArgs,
        string? reportPath,
        string? tracearrServer,
        string? plexDbPath,
        CancellationToken cancellationToken)
    {
        var sourceLower = source.ToLowerInvariant();
        if (scope.History && sourceLower is "plex" or "jellyfin")
        {
            AnsiConsole.MarkupLine("[yellow]History import is disabled for direct Plex/Jellyfin sources (no per-play timestamps). Use Tautulli or Tracearr for history. Continuing with ratings and playlists only.[/]");
            scope = scope with { History = false };
        }

        if (sourceLower != "spotify" && string.IsNullOrEmpty(sourceApiKey))
            throw new ArgumentException("--source-api-key is required for this source.");

        if (!string.IsNullOrWhiteSpace(tracearrServer) && sourceLower != "tracearr")
            throw new ArgumentException("--tracearr-server is only valid with --source tracearr.");

        if (!string.IsNullOrWhiteSpace(plexDbPath) && sourceLower != "plex")
            throw new ArgumentException("--plex-db is only valid with --source plex.");

        ISourceClient sourceClient = sourceLower switch
        {
            "plex" => new PlexClient(sourceUrl, sourceApiKey, plexDbPath) { IncludeDynamicPlaylists = includeDynamicPlaylists },
            "jellyfin" => new JellyfinClient(sourceUrl, sourceApiKey),
            "tautulli" => new TautulliClient(sourceUrl, sourceApiKey),
            "tracearr" => new TracearrClient(sourceUrl, sourceApiKey, tracearrServer),
            "spotify" => new SpotifyClient(sourceApiKey, spotifyDataDir),
            _ => throw new ArgumentException($"Unknown source: {source}. Use 'plex', 'jellyfin', 'tautulli', 'tracearr', or 'spotify'.")
        };

        AnsiConsole.MarkupLine("[bold]Connecting to source server...[/]");
        var serverInfo = await sourceClient.ValidateConnectionAsync(cancellationToken);
        AnsiConsole.MarkupLine($"[green]Connected to {serverInfo.Name} (v{serverInfo.Version})[/]");

        if (sourceClient is SpotifyClient spotifyWarnings)
        {
            foreach (var warning in spotifyWarnings.TokenWarnings)
                AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(warning)}[/]");
        }

        if (sourceClient is TracearrClient tracearrClient)
        {
            if (tracearrClient.Servers.Count == 0)
            {
                AnsiConsole.MarkupLine("[yellow]Tracearr reported no media servers.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[bold]Tracearr media servers:[/]");
                foreach (var server in tracearrClient.Servers)
                    AnsiConsole.MarkupLine($"  - {Markup.Escape(server.Type)}  {Markup.Escape(server.Id)}");

                if (!string.IsNullOrWhiteSpace(tracearrServer))
                    AnsiConsole.MarkupLine($"[dim]Filtering with --tracearr-server {Markup.Escape(tracearrServer)}[/]");
                else
                    AnsiConsole.MarkupLine("[dim]Tip: pass --tracearr-server plex|jellyfin|emby (or a server UUID) to import one backend only.[/]");
            }
        }
        if (sourceClient is SpotifyClient spotifyClient && !string.IsNullOrEmpty(spotifyDataDir))
        {
            if (scope.History && !spotifyClient.HasStreamingHistoryExport())
            {
                AnsiConsole.MarkupLine("[yellow]No streaming history JSON found in --spotify-data-dir (expected endsong_*/StreamingHistory_* with ms_played). History import will be empty. Playlist*.json alone is account data, not listen history.[/]");
            }

            if (scope.Playlists && string.IsNullOrEmpty(sourceApiKey) && !spotifyClient.HasPlaylistExport())
            {
                AnsiConsole.MarkupLine("[yellow]No Playlist*.json found in --spotify-data-dir and no API token provided. Playlist import will be empty.[/]");
            }
        }

        AnsiConsole.MarkupLine("[bold]Authenticating with K7...[/]");
        var authenticator = new DeviceCodeAuthenticator(k7Url);
        await authenticator.AuthenticateAsync(cancellationToken);
        AnsiConsole.MarkupLine("[green]Authenticated with K7.[/]");

        var k7Client = new K7ApiClient(k7Url, authenticator.AccessToken!);
        var report = new ImportReport { DryRun = dryRun };

        var sourceUsers = await sourceClient.GetUsersAsync(cancellationToken);
        if (sourceClient is PlexClient plexClient)
        {
            foreach (var warning in plexClient.TokenWarnings)
            {
                report.Warnings.Add(warning);
                AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(warning)}[/]");
            }
        }

        var userFilter = SourceUserFilter.Apply(sourceUsers, userFilters);
        if (userFilter.IsActive)
        {
            AnsiConsole.MarkupLine(
                $"[dim]--users: kept {userFilter.Kept.Count}, skipped {userFilter.Excluded.Count}[/]");
            foreach (var user in userFilter.Kept)
            {
                AnsiConsole.MarkupLine(
                    $"  [green]kept[/] {Markup.Escape(user.DisplayName)} (id: {Markup.Escape(user.Id)})");
            }

            foreach (var filter in userFilter.UnmatchedFilters)
            {
                AnsiConsole.MarkupLine(
                    $"[yellow]--users '{Markup.Escape(filter)}' matched no source user.[/]");
            }

            sourceUsers = userFilter.Kept.ToList();
        }

        var k7Users = await k7Client.GetUsersAsync(cancellationToken);
        var parsedMappings = ParseUserMappings(userMappings);
        var userPlans = await ResolveUserMappingsAsync(
            sourceUsers, k7Users, parsedMappings, k7Client, sourceLower, dryRun, autoMapUsers, cancellationToken);
        report.Users.AddRange(userPlans);

        var activePlans = userPlans.Where(p => p.Kind is not UserMappingKind.Skipped).ToList();
        if (activePlans.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No users to import. Exiting.[/]");
            ImportReportPrinter.Print(report, scope, reportPath);
            return;
        }

        var libraries = await sourceClient.GetLibrariesAsync(cancellationToken);
        if (sourceLower == "spotify" && !string.IsNullOrEmpty(spotifyDataDir))
            libraries = libraries.Where(l => l.Id != "recently-played").ToList();

        AnsiConsole.MarkupLine($"[bold]Found {libraries.Count} libraries:[/]");
        foreach (var lib in libraries)
            AnsiConsole.MarkupLine($"  - {lib.Name} ({lib.MediaType ?? "unknown"})");

        var pathMaps = MediaMatcher.ParsePathMaps(pathMapArgs);
        var spotifyIdBridge = sourceLower == "spotify"
            ? new SpotifyIdBridge(spotifyDataDir is not null
                ? Path.Combine(spotifyDataDir, "k7-spotify-id-bridge.json")
                : null)
            : null;
        var matcher = new MediaMatcher(k7Client, pathMaps, spotifyIdBridge);
        var deviceResolver = new ImportDeviceResolver(k7Client);

        var userLibraryItems = new Dictionary<string, Dictionary<string, List<SourceMediaItem>>>();
        var libraryMatches = new Dictionary<string, Dictionary<string, Guid>>();
        var libraryItemStatus = new Dictionary<string, Dictionary<string, MatchStatus>>();

        if (scope.History || scope.Ratings)
        {
            await AnsiConsole.Status()
                .StartAsync("Collecting user interactions...", async ctx =>
                {
                    var interactedItemsPerLibrary = new Dictionary<string, Dictionary<string, SourceMediaItem>>();

                    foreach (var plan in activePlans)
                    {
                        var sourceUser = plan.Source;
                        userLibraryItems[sourceUser.Id] = [];

                        foreach (var library in libraries)
                        {
                            var userLabel = Markup.Escape(sourceUser.DisplayName);
                            var libraryLabel = Markup.Escape(library.Name);
                            ctx.Status($"Fetching {library.Name} for {sourceUser.DisplayName}...");

                            var progress = new Progress<string>(detail =>
                                ctx.Status($"Fetching {library.Name} for {sourceUser.DisplayName} - {detail}"));

                            List<SourceMediaItem> allItems;
                            try
                            {
                                allItems = await sourceClient.GetLibraryItemsAsync(
                                    library.Id, sourceUser.Id, progress, cancellationToken);
                            }
                            catch (Exception ex)
                            {
                                AnsiConsole.MarkupLine(
                                    $"[yellow]Skipping {libraryLabel} for {userLabel}: {Markup.Escape(ex.Message)}[/]");
                                allItems = [];
                            }

                            var filtered = allItems.Where(i =>
                                (scope.History && (i.PlayCount > 0 || i.IsCompleted || i.PlayHistory.Count > 0))
                                || (scope.Ratings && i.Rating is > 0)
                            ).ToList();

                            userLibraryItems[sourceUser.Id][library.Id] = filtered;
                            AnsiConsole.MarkupLine(
                                $"[dim]Fetched {filtered.Count} interacted item(s) from {libraryLabel} for {userLabel}[/]");

                            if (!interactedItemsPerLibrary.ContainsKey(library.Id))
                                interactedItemsPerLibrary[library.Id] = [];

                            foreach (var item in filtered)
                                interactedItemsPerLibrary[library.Id].TryAdd(item.Id, item);
                        }
                    }

                    if (pathMaps.Count == 0)
                    {
                        var sampleItems = interactedItemsPerLibrary.Values
                            .SelectMany(d => d.Values)
                            .Where(i => i.FilePaths.Count > 0)
                            .Take(500)
                            .ToList();
                        if (sampleItems.Count > 0)
                        {
                            ctx.Status("Auto-deducing path prefix maps...");
                            var deduced = await matcher.AutoDeducePathMapsAsync(sampleItems, cancellationToken);
                            if (deduced.Count > 0)
                            {
                                pathMaps = pathMaps.Concat(deduced).ToList();
                                matcher = new MediaMatcher(k7Client, pathMaps, spotifyIdBridge);
                            }
                        }
                    }
                    else
                    {
                        foreach (var (plexPrefix, k7Prefix) in pathMaps)
                            AnsiConsole.MarkupLine($"[dim]Path map: {Markup.Escape(plexPrefix)} => {Markup.Escape(k7Prefix)}[/]");
                    }

                    foreach (var library in libraries)
                    {
                        if (!interactedItemsPerLibrary.TryGetValue(library.Id, out var interacted) || interacted.Count == 0)
                        {
                            libraryMatches[library.Id] = [];
                            libraryItemStatus[library.Id] = [];
                            continue;
                        }

                        var itemsToMatch = interacted.Values.ToList();
                        ctx.Status($"Matching {itemsToMatch.Count} interacted items from {library.Name}...");
                        var outcome = await matcher.MatchItemsAsync(
                            itemsToMatch,
                            createMissing: createMissing,
                            fetchMetadata: fetchMetadata,
                            dryRun: dryRun,
                            progress: new Progress<string>(msg => ctx.Status(msg)),
                            cancellationToken);

                        report.MergeMedia(outcome);
                        libraryMatches[library.Id] = outcome.Matches;
                        libraryItemStatus[library.Id] = outcome.ItemResults
                            .ToDictionary(r => r.Item.Id, r => r.Status, StringComparer.Ordinal);

                        AnsiConsole.MarkupLine(
                            $"[dim]Matched {library.Name}: {outcome.Matches.Count} existing, " +
                            $"{(dryRun ? outcome.WouldCreateCount : outcome.CreatedCount)} " +
                            $"{(dryRun ? "would create" : "created")}, {outcome.UnmatchedCount} unmatched[/]");
                    }
                });
        }

        foreach (var plan in activePlans)
        {
            var sourceUser = plan.Source;
            var preview = new UserImportPreview { Plan = plan };
            report.PerUser.Add(preview);

            AnsiConsole.MarkupLine($"\n[bold blue]{(dryRun ? "Previewing" : "Importing")} data for {sourceUser.DisplayName}...[/]");

            await AnsiConsole.Status()
                .StartAsync(dryRun ? "Previewing user data..." : "Importing user data...", async ctx =>
                {
                    if (scope.History || scope.Ratings)
                    {
                        foreach (var library in libraries)
                        {
                            if (!userLibraryItems.TryGetValue(sourceUser.Id, out var libItems)
                                || !libItems.TryGetValue(library.Id, out var items)
                                || items.Count == 0)
                                continue;

                            libraryMatches.TryGetValue(library.Id, out var matches);
                            matches ??= [];
                            libraryItemStatus.TryGetValue(library.Id, out var statuses);
                            statuses ??= [];

                            if (scope.History)
                            {
                                var historyItems = items
                                    .Where(i => i.PlayCount > 0 || i.IsCompleted || i.PlayHistory.Count > 0)
                                    .ToList();
                                AccumulateMediaCounts(historyItems, statuses, dryRun,
                                    out var matched, out var wouldCreate, out var created, out var unmatched);
                                preview.HistorySourceItems += historyItems.Count;
                                preview.HistoryMatched += matched;
                                preview.HistoryWouldCreate += wouldCreate;
                                preview.HistoryCreated += created;
                                preview.HistoryUnmatched += unmatched;

                                var stateItems = historyItems
                                    .Where(i => matches.ContainsKey(i.Id))
                                    .GroupBy(i => matches[i.Id])
                                    .Select(g =>
                                    {
                                        var latest = g.MaxBy(i => i.LastPlayedAt ?? DateTime.MinValue) ?? g.First();
                                        return new BulkUpsertMediaStatesRequest.MediaStateItem
                                        {
                                            MediaId = g.Key,
                                            PlayCount = g.Sum(i => i.PlayCount),
                                            LastPlaybackPosition = latest.LastPlaybackPosition ?? 0,
                                            ProgressPercentage = CalculateProgressPercentage(latest),
                                            IsCompleted = g.Any(i => i.IsCompleted),
                                            LastInteractedAt = g.Max(i => i.LastPlayedAt)
                                        };
                                    })
                                    .ToList();

                                var sessionCount = historyItems
                                    .Where(i => matches.ContainsKey(i.Id))
                                    .Sum(i => i.PlayHistory.Count);

                                if (dryRun)
                                {
                                    preview.WatchStates += stateItems.Count;
                                    preview.PlaybackSessions += sessionCount;
                                }
                                else
                                {
                                    var sessionItems = await BuildPlaybackSessionItemsAsync(
                                        historyItems, matches, deviceResolver, cancellationToken);

                                    if (stateItems.Count > 0)
                                    {
                                        ctx.Status($"Importing {stateItems.Count} watch states for {sourceUser.Name}...");
                                        preview.WatchStates += await k7Client.BulkUpsertMediaStatesAsync(
                                            plan.K7UserId, stateItems, strategy, cancellationToken);
                                    }

                                    if (sessionItems.Count > 0)
                                    {
                                        ctx.Status($"Importing {sessionItems.Count} playback sessions for {sourceUser.Name}...");
                                        preview.PlaybackSessions += await k7Client.BulkCreatePlaybackSessionsAsync(
                                            plan.K7UserId, sessionItems, cancellationToken);
                                    }
                                }
                            }

                            if (scope.Ratings)
                            {
                                var ratingSource = items.Where(i => i.Rating is > 0).ToList();
                                AccumulateMediaCounts(ratingSource, statuses, dryRun,
                                    out var matched, out var wouldCreate, out var created, out var unmatched);
                                preview.RatingsSourceItems += ratingSource.Count;
                                preview.RatingsMatched += matched;
                                preview.RatingsWouldCreate += wouldCreate;
                                preview.RatingsCreated += created;
                                preview.RatingsUnmatched += unmatched;

                                var ratingItems = ratingSource
                                    .Where(i => matches.ContainsKey(i.Id))
                                    .Select(i => new BulkUpsertRatingsRequest.RatingItem
                                    {
                                        MediaId = matches[i.Id],
                                        Value = i.Rating!.Value
                                    })
                                    .DistinctBy(i => i.MediaId)
                                    .ToList();

                                if (dryRun)
                                {
                                    preview.RatingsToImport += ratingItems.Count;
                                }
                                else if (ratingItems.Count > 0)
                                {
                                    ctx.Status($"Importing {ratingItems.Count} ratings for {sourceUser.Name}...");
                                    preview.RatingsToImport += await k7Client.BulkUpsertRatingsAsync(
                                        plan.K7UserId, ratingItems, strategy, cancellationToken);
                                }
                            }
                        }
                    }

                    if (!scope.Playlists)
                        return;

                    ctx.Status("Fetching playlists...");
                    var playlists = await sourceClient.GetPlaylistsAsync(sourceUser.Id, cancellationToken);
                    AnsiConsole.MarkupLine($"[dim]Found {playlists.Count} playlist(s) for {Markup.Escape(sourceUser.Name)}[/]");

                    foreach (var playlist in playlists)
                    {
                        if (playlist.IsDynamic && !includeDynamicPlaylists)
                        {
                            preview.SkippedDynamicPlaylists++;
                            preview.Playlists.Add(new PlaylistPreview
                            {
                                Title = playlist.Title,
                                IsDynamic = true,
                                Skipped = true,
                                SkipReason = "dynamic playlist",
                                SourceItems = playlist.Items.Count
                            });
                            continue;
                        }

                        var defaultMediaType = playlist.MediaType ?? "music";
                        ctx.Status($"Matching playlist '{playlist.Title}' ({playlist.Items.Count} items)...");
                        var outcome = await matcher.MatchPlaylistItemsAsync(
                            playlist.Items,
                            defaultMediaType,
                            createMissing: createMissing,
                            fetchMetadata: fetchMetadata && createMissing && !dryRun,
                            dryRun: dryRun,
                            cancellationToken);

                        report.MergeMedia(outcome);

                        var playlistPreview = new PlaylistPreview
                        {
                            Title = playlist.Title,
                            IsDynamic = playlist.IsDynamic,
                            SourceItems = playlist.Items.Count,
                            Matched = outcome.MatchedExisting.Count(),
                            WouldCreate = outcome.WouldCreateCount,
                            Created = outcome.CreatedCount,
                            Unmatched = outcome.UnmatchedCount
                        };
                        preview.Playlists.Add(playlistPreview);

                        AnsiConsole.MarkupLine(
                            $"[dim]Playlist '{Markup.Escape(playlist.Title)}': {playlist.Items.Count} source, " +
                            $"{playlistPreview.Matched} matched, " +
                            $"{(dryRun ? playlistPreview.WouldCreate : playlistPreview.Created)} " +
                            $"{(dryRun ? "would create" : "created")}, {playlistPreview.Unmatched} unmatched[/]");

                        if (dryRun || outcome.Matches.Count == 0)
                            continue;

                        var playlistMediaType = playlist.MediaType switch
                        {
                            "music" => MediaType.MusicTrack,
                            _ => null as MediaType?
                        };

                        if (playlistMediaType is null)
                        {
                            var firstMatchedMediaId = outcome.Matches.Values.First();
                            var firstMatchedType = await k7Client.GetMediaTypeAsync(firstMatchedMediaId, cancellationToken);
                            playlistMediaType = firstMatchedType switch
                            {
                                MediaType.Movie => MediaType.Movie,
                                MediaType.SerieEpisode => MediaType.SerieEpisode,
                                MediaType.MusicTrack => MediaType.MusicTrack,
                                _ => MediaType.Movie
                            };
                        }

                        var sourceLabel = sourceLower switch
                        {
                            "plex" => "Plex",
                            "jellyfin" => "Jellyfin",
                            "spotify" => "Spotify",
                            "tautulli" => "Tautulli",
                            "tracearr" => "Tracearr",
                            _ => char.ToUpperInvariant(sourceLower[0]) + sourceLower[1..]
                        };
                        var playlistTitle = playlist.Title.StartsWith($"{sourceLabel} - ", StringComparison.OrdinalIgnoreCase)
                            ? playlist.Title
                            : $"{sourceLabel} - {playlist.Title}";

                        ctx.Status($"Importing playlist '{playlistTitle}' ({outcome.Matches.Count}/{playlist.Items.Count})...");
                        var mediaIds = playlist.Items
                            .Where(i => outcome.Matches.ContainsKey(i.Id))
                            .Select(i => outcome.Matches[i.Id])
                            .ToList();

                        await k7Client.ImportUserPlaylistAsync(
                            plan.K7UserId,
                            playlistTitle,
                            playlistMediaType.Value,
                            mediaIds,
                            cancellationToken);
                    }
                });
        }

        var skippedScopes = new List<string>();
        if (!scope.History) skippedScopes.Add("history");
        if (!scope.Ratings) skippedScopes.Add("ratings");
        if (!scope.Playlists) skippedScopes.Add("playlists");
        if (skippedScopes.Count > 0)
            AnsiConsole.MarkupLine($"[dim]Skipped scopes: {string.Join(", ", skippedScopes)}[/]");

        ImportReportPrinter.Print(report, scope, reportPath);
    }

    private static void AccumulateMediaCounts(
        IReadOnlyList<SourceMediaItem> items,
        IReadOnlyDictionary<string, MatchStatus> statuses,
        bool dryRun,
        out int matched,
        out int wouldCreate,
        out int created,
        out int unmatched)
    {
        matched = 0;
        wouldCreate = 0;
        created = 0;
        unmatched = 0;

        var wouldCreateKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var createdKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            if (!statuses.TryGetValue(item.Id, out var status))
            {
                unmatched++;
                continue;
            }

            switch (status)
            {
                case MatchStatus.MatchedByExternalId:
                case MatchStatus.MatchedByPath:
                case MatchStatus.MatchedByTitle:
                    matched++;
                    break;
                case MatchStatus.WouldCreate:
                    if (item.MediaType == "music")
                    {
                        if (wouldCreateKeys.Add(MusicItemCollapser.IdentityKey(item)))
                            wouldCreate++;
                    }
                    else
                    {
                        wouldCreate++;
                    }

                    break;
                case MatchStatus.Created:
                    if (item.MediaType == "music")
                    {
                        if (createdKeys.Add(MusicItemCollapser.IdentityKey(item)))
                            created++;
                    }
                    else
                    {
                        created++;
                    }

                    break;
                default:
                    unmatched++;
                    break;
            }
        }

        if (!dryRun)
            wouldCreate = 0;
    }

    private static async Task<List<BulkCreatePlaybackSessionsRequest.PlaybackSessionItem>> BuildPlaybackSessionItemsAsync(
        List<SourceMediaItem> items,
        Dictionary<string, Guid> matches,
        ImportDeviceResolver deviceResolver,
        CancellationToken cancellationToken)
    {
        var playEntries = items
            .Where(i => matches.ContainsKey(i.Id) && i.PlayHistory.Count > 0)
            .SelectMany(i => i.PlayHistory.Select(p => (MediaId: matches[i.Id], Entry: p)))
            .ToList();

        if (playEntries.Count == 0)
            return [];

        var deviceMap = await deviceResolver.ResolveDevicesAsync(
            playEntries.Select(x => x.Entry),
            cancellationToken);

        return playEntries.Select(x =>
        {
            var entry = x.Entry;
            var deviceKey = ImportDeviceResolver.BuildDeviceKey(entry);
            deviceMap.TryGetValue(deviceKey, out var deviceId);

            return new BulkCreatePlaybackSessionsRequest.PlaybackSessionItem
            {
                MediaId = x.MediaId,
                StartedAt = entry.PlayedAt,
                DurationSeconds = entry.DurationSeconds,
                WatchedDurationSeconds = entry.DurationSeconds,
                IsCompleted = entry.IsCompleted,
                DeviceId = deviceId == Guid.Empty ? null : deviceId,
                IsTranscode = entry.IsTranscode,
                VideoDecision = entry.VideoDecision,
                AudioDecision = entry.AudioDecision,
                Bitrate = entry.Bitrate,
                SourceVideoCodec = entry.SourceVideoCodec,
                SourceAudioCodec = entry.SourceAudioCodec,
                SourceVideoWidth = entry.SourceVideoWidth,
                SourceVideoHeight = entry.SourceVideoHeight,
                StreamVideoCodec = entry.StreamVideoCodec,
                StreamAudioCodec = entry.StreamAudioCodec
            };
        }).ToList();
    }

    private static double CalculateProgressPercentage(SourceMediaItem item)
    {
        if (item.IsCompleted)
            return 100;

        if (item.ProgressPercentage is > 0)
            return Math.Clamp(item.ProgressPercentage.Value, 0, 99.9);

        if (item.DurationSeconds is > 0 && item.LastPlaybackPosition is > 0)
            return Math.Min(100.0, item.LastPlaybackPosition.Value / item.DurationSeconds.Value * 100.0);

        if (item.PlayCount > 0 || item.LastPlayedAt is not null)
            return 50;

        return 0;
    }

    private static Dictionary<string, string> ParseUserMappings(string[] mappings)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings)
        {
            var parts = mapping.Split(':', 2);
            if (parts.Length == 2)
                result[parts[0].Trim()] = parts[1].Trim();
        }

        return result;
    }

    private static async Task<List<UserPlan>> ResolveUserMappingsAsync(
        List<SourceUser> sourceUsers,
        List<K7.Shared.Dtos.Users.UserDto> k7Users,
        Dictionary<string, string> explicitMappings,
        K7ApiClient k7Client,
        string sourceType,
        bool dryRun,
        bool autoMapUsers,
        CancellationToken cancellationToken)
    {
        var plans = new List<UserPlan>();

        if (sourceUsers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No users found on source server.[/]");
            return plans;
        }

        AnsiConsole.MarkupLine($"\n[bold]Source users ({sourceUsers.Count}):[/]");
        foreach (var user in sourceUsers)
            AnsiConsole.MarkupLine($"  - {user.DisplayName} (id: {user.Id})");

        AnsiConsole.MarkupLine($"\n[bold]K7 users ({k7Users.Count}):[/]");
        foreach (var user in k7Users)
            AnsiConsole.MarkupLine($"  - {user.UserName} (id: {user.Id})");

        foreach (var sourceUser in sourceUsers)
        {
            if (string.IsNullOrWhiteSpace(sourceUser.Name))
            {
                plans.Add(new UserPlan
                {
                    Source = sourceUser,
                    K7UserId = Guid.Empty,
                    TargetUsername = "(none)",
                    Kind = UserMappingKind.Skipped,
                    SkipReason = "empty name"
                });
                AnsiConsole.MarkupLine($"[yellow]Skipping source user with empty name (id: {sourceUser.Id}).[/]");
                continue;
            }

            if (explicitMappings.TryGetValue(sourceUser.Name, out var k7Username))
            {
                var k7User = k7Users.FirstOrDefault(u =>
                    string.Equals(u.UserName, k7Username, StringComparison.OrdinalIgnoreCase));

                if (k7User is not null)
                {
                    plans.Add(new UserPlan
                    {
                        Source = sourceUser,
                        K7UserId = k7User.Id,
                        TargetUsername = k7User.UserName ?? k7Username,
                        Kind = UserMappingKind.MappedExisting
                    });
                    AnsiConsole.MarkupLine($"[green]Mapped {sourceUser.Name} -> {k7User.UserName}[/]");
                }
                else
                {
                    plans.Add(new UserPlan
                    {
                        Source = sourceUser,
                        K7UserId = Guid.Empty,
                        TargetUsername = k7Username,
                        Kind = UserMappingKind.Skipped,
                        SkipReason = $"K7 user '{k7Username}' not found"
                    });
                    AnsiConsole.MarkupLine($"[yellow]K7 user '{k7Username}' not found. Skipping {sourceUser.Name}.[/]");
                }

                continue;
            }

            if (autoMapUsers)
            {
                var exactMatch = k7Users.FirstOrDefault(u =>
                    string.Equals(u.UserName, sourceUser.Name, StringComparison.OrdinalIgnoreCase));
                if (exactMatch is not null)
                {
                    plans.Add(new UserPlan
                    {
                        Source = sourceUser,
                        K7UserId = exactMatch.Id,
                        TargetUsername = exactMatch.UserName ?? sourceUser.Name,
                        Kind = UserMappingKind.AutoMapped
                    });
                    AnsiConsole.MarkupLine($"[green]Auto-mapped {sourceUser.Name} -> {exactMatch.UserName}[/]");
                    continue;
                }
            }

            var tempUsername = $"{sourceType}-{sourceUser.Name.ToLowerInvariant().Replace(' ', '-')}";
            var existingTemp = k7Users.FirstOrDefault(u =>
                string.Equals(u.UserName, tempUsername, StringComparison.OrdinalIgnoreCase));

            if (existingTemp is not null)
            {
                plans.Add(new UserPlan
                {
                    Source = sourceUser,
                    K7UserId = existingTemp.Id,
                    TargetUsername = tempUsername,
                    Kind = UserMappingKind.ReuseTemp
                });
                AnsiConsole.MarkupLine($"[dim]No mapping for {sourceUser.Name}, reusing temp user '{tempUsername}'[/]");
                continue;
            }

            if (dryRun)
            {
                plans.Add(new UserPlan
                {
                    Source = sourceUser,
                    K7UserId = Guid.NewGuid(),
                    TargetUsername = tempUsername,
                    Kind = UserMappingKind.WouldCreateTemp
                });
                AnsiConsole.MarkupLine($"[dim]No mapping for {sourceUser.Name}, would create temp user '{tempUsername}'[/]");
                continue;
            }

            var created = await k7Client.CreateUserAsync(tempUsername, "User", cancellationToken);
            k7Users.Add(created);
            plans.Add(new UserPlan
            {
                Source = sourceUser,
                K7UserId = created.Id,
                TargetUsername = tempUsername,
                Kind = UserMappingKind.CreatedTemp
            });
            AnsiConsole.MarkupLine($"[green]Created temp K7 user '{tempUsername}'[/]");
        }

        return plans;
    }

    private static ImportScope ParseIncludeScope(string[]? include)
    {
        if (include is null || include.Length == 0)
            return new ImportScope(true, true, true);

        var set = new HashSet<string>(include.Select(s => s.ToLowerInvariant()));
        return new ImportScope(
            History: set.Contains("history"),
            Ratings: set.Contains("ratings"),
            Playlists: set.Contains("playlists"));
    }
}
