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
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Preview changes without applying them" };
        var includeOption = new Option<string[]>("--include") { Description = "Data types to import: history, ratings, playlists (default: all)", AllowMultipleArgumentsPerToken = true };
        var spotifyDataDirOption = new Option<string>("--spotify-data-dir") { Description = "Path to Spotify extended streaming history export folder (JSON files)" };
        var userMappingOption = new Option<string[]>("--user-mapping") { Description = "Map source user to K7 user (format: sourceUser:k7User)", AllowMultipleArgumentsPerToken = true };
        var createMissingOption = new Option<bool>("--create-missing") { Description = "Create virtual media entities for unmatched items (default: true)", DefaultValueFactory = _ => true };
        var noCreateMissingOption = new Option<bool>("--no-create-missing") { Description = "Disable virtual media creation for unmatched items" };
        var playcountModeOption = new Option<string>("--playcount-mode") { Description = "PlayCount merge strategy: additive or max (default: additive)", DefaultValueFactory = _ => "additive" };
        var ratingModeOption = new Option<string>("--rating-mode") { Description = "Rating conflict strategy: keep or overwrite (default: keep)", DefaultValueFactory = _ => "keep" };
        var progressModeOption = new Option<string>("--progress-mode") { Description = "Progress conflict strategy: recent or overwrite (default: recent)", DefaultValueFactory = _ => "recent" };

        var command = new RootCommand("K7 Import Tool — Import media data from Plex, Jellyfin, Tautulli, Tracearr, or Spotify into K7");
        command.Add(sourceOption);
        command.Add(sourceUrlOption);
        command.Add(sourceApiKeyOption);
        command.Add(k7UrlOption);
        command.Add(dryRunOption);
        command.Add(includeOption);
        command.Add(spotifyDataDirOption);
        command.Add(userMappingOption);
        command.Add(createMissingOption);
        command.Add(noCreateMissingOption);
        command.Add(playcountModeOption);
        command.Add(ratingModeOption);
        command.Add(progressModeOption);

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
            var scope = ParseIncludeScope(include);
            var createMissing = parseResult.GetValue(createMissingOption) && !parseResult.GetValue(noCreateMissingOption);

            var strategy = new MergeStrategy
            {
                PlayCount = parseResult.GetValue(playcountModeOption) == "max"
                    ? PlayCountMergeMode.Max : PlayCountMergeMode.Additive,
                Rating = parseResult.GetValue(ratingModeOption) == "overwrite"
                    ? RatingConflictMode.Overwrite : RatingConflictMode.KeepExisting,
                Progress = parseResult.GetValue(progressModeOption) == "overwrite"
                    ? ProgressConflictMode.AlwaysOverwrite : ProgressConflictMode.MostRecent
            };

            await RunAsync(source, sourceUrl, sourceApiKey, k7Url, dryRun, scope, spotifyDataDir, userMapping, createMissing, strategy);
        });

        return command;
    }

    private static async Task RunAsync(string source, string sourceUrl, string sourceApiKey, string k7Url, bool dryRun, ImportScope scope, string? spotifyDataDir, string[] userMappings, bool createMissing, MergeStrategy strategy)
    {
        var cancellationToken = CancellationToken.None;

        var sourceLower = source.ToLowerInvariant();
        if (scope.History && sourceLower is "plex" or "jellyfin")
        {
            AnsiConsole.MarkupLine("[yellow]History import is disabled for direct Plex/Jellyfin sources (no per-play timestamps). Use Tautulli or Tracearr for history. Continuing with ratings and playlists only.[/]");
            scope = scope with { History = false };
        }

        // 1. Create source client
        if (sourceLower != "spotify" && string.IsNullOrEmpty(sourceApiKey))
            throw new ArgumentException("--source-api-key is required for this source.");

        ISourceClient sourceClient = sourceLower switch
        {
            "plex" => new PlexClient(sourceUrl, sourceApiKey),
            "jellyfin" => new JellyfinClient(sourceUrl, sourceApiKey),
            "tautulli" => new TautulliClient(sourceUrl, sourceApiKey),
            "tracearr" => new TracearrClient(sourceUrl, sourceApiKey),
            "spotify" => new SpotifyClient(sourceApiKey, spotifyDataDir),
            _ => throw new ArgumentException($"Unknown source: {source}. Use 'plex', 'jellyfin', 'tautulli', 'tracearr', or 'spotify'.")
        };

        // 2. Validate source connection
        AnsiConsole.MarkupLine("[bold]Connecting to source server...[/]");
        var serverInfo = await sourceClient.ValidateConnectionAsync(cancellationToken);
        AnsiConsole.MarkupLine($"[green]Connected to {serverInfo.Name} (v{serverInfo.Version})[/]");

        // 3. Authenticate with K7
        AnsiConsole.MarkupLine("[bold]Authenticating with K7...[/]");
        var authenticator = new DeviceCodeAuthenticator(k7Url);
        await authenticator.AuthenticateAsync(cancellationToken);
        AnsiConsole.MarkupLine("[green]Authenticated with K7.[/]");

        var k7Client = new K7ApiClient(k7Url, authenticator.AccessToken!);

        // 4. Get users from source and K7
        var sourceUsers = await sourceClient.GetUsersAsync(cancellationToken);
        var k7Users = await k7Client.GetUsersAsync(cancellationToken);

        // 5. Resolve user mappings
        var parsedMappings = ParseUserMappings(userMappings);
        var userMap = await ResolveUserMappingsAsync(
            sourceUsers, k7Users, parsedMappings, k7Client, source, dryRun, cancellationToken);

        if (userMap.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No users to import. Exiting.[/]");
            return;
        }

        // 6. Get libraries
        var libraries = await sourceClient.GetLibrariesAsync(cancellationToken);
        AnsiConsole.MarkupLine($"[bold]Found {libraries.Count} libraries:[/]");
        foreach (var lib in libraries)
        {
            AnsiConsole.MarkupLine($"  - {lib.Name} ({lib.MediaType ?? "unknown"})");
        }

        // 7. Collect items with interactions and match once
        var matcher = new MediaMatcher(k7Client);
        var totalResult = new ImportResult();

        var userLibraryItems = new Dictionary<string, Dictionary<string, List<SourceMediaItem>>>();
        var libraryMatches = new Dictionary<string, Dictionary<string, Guid>>();

        if (scope.History || scope.Ratings)
        {
            await AnsiConsole.Status()
                .StartAsync("Collecting user interactions...", async ctx =>
                {
                    var interactedItemsPerLibrary = new Dictionary<string, Dictionary<string, SourceMediaItem>>();

                    foreach (var (sourceUser, _) in userMap)
                    {
                        userLibraryItems[sourceUser.Id] = [];

                        foreach (var library in libraries)
                        {
                            ctx.Status($"Fetching {library.Name} for {sourceUser.Name}...");
                            var allItems = await sourceClient.GetLibraryItemsAsync(library.Id, sourceUser.Id, cancellationToken);

                            var filtered = allItems.Where(i =>
                                (scope.History && (i.PlayCount > 0 || i.IsCompleted || i.PlayHistory.Count > 0))
                                || (scope.Ratings && i.Rating is > 0)
                            ).ToList();

                            userLibraryItems[sourceUser.Id][library.Id] = filtered;

                            if (!interactedItemsPerLibrary.ContainsKey(library.Id))
                                interactedItemsPerLibrary[library.Id] = [];

                            foreach (var item in filtered)
                                interactedItemsPerLibrary[library.Id].TryAdd(item.Id, item);
                        }
                    }

                    foreach (var library in libraries)
                    {
                        if (!interactedItemsPerLibrary.TryGetValue(library.Id, out var interacted) || interacted.Count == 0)
                        {
                            libraryMatches[library.Id] = [];
                            continue;
                        }

                        var itemsToMatch = interacted.Values.ToList();
                        ctx.Status($"Matching {itemsToMatch.Count} interacted items from {library.Name}...");
                        var matches = await matcher.MatchItemsAsync(itemsToMatch, cancellationToken);

                        var unmatched = itemsToMatch.Where(i => !matches.ContainsKey(i.Id)).ToList();
                        totalResult.MatchedItems += matches.Count;

                        if (createMissing && unmatched.Count > 0 && !dryRun)
                        {
                            ctx.Status($"Creating {unmatched.Count} missing media entities...");
                            var createItems = unmatched.Select(i => new BulkCreateMediasRequest.BulkCreateMediaItem
                            {
                                Key = i.Id,
                                MediaType = i.MediaType ?? "music",
                                Title = i.Title,
                                Year = i.Year,
                                ExternalIds = i.ProviderIds,
                                ArtistName = i.ArtistName,
                                AlbumName = i.AlbumName,
                                SeriesTitle = i.SeriesTitle,
                                SeasonNumber = i.SeasonNumber,
                                EpisodeNumber = i.EpisodeNumber
                            }).ToList();

                            var createResult = await k7Client.BulkCreateMediasAsync(createItems, cancellationToken);

                            foreach (var r in createResult.Results)
                            {
                                matches.TryAdd(r.Key, r.MediaId);
                                if (r.WasCreated)
                                    totalResult.CreatedMedias++;
                            }

                            totalResult.MatchedItems += createResult.Results.Count;
                        }

                        totalResult.UnmatchedItems += itemsToMatch.Count(i => !matches.ContainsKey(i.Id));
                        foreach (var item in itemsToMatch.Where(i => !matches.ContainsKey(i.Id)))
                            totalResult.UnmatchedTitles.Add($"{item.Title} ({item.Year})");

                        libraryMatches[library.Id] = matches;
                    }
                });
        }

        // 8. Import per user using pre-computed matches
        foreach (var (sourceUser, k7UserId) in userMap)
        {
            AnsiConsole.MarkupLine($"\n[bold blue]Importing data for {sourceUser.Name}...[/]");

            await AnsiConsole.Status()
                .StartAsync("Importing user data...", async ctx =>
                {
                    if (!dryRun && (scope.History || scope.Ratings))
                    {
                        foreach (var library in libraries)
                        {
                            if (!libraryMatches.TryGetValue(library.Id, out var matches) || matches.Count == 0)
                                continue;

                            if (!userLibraryItems.TryGetValue(sourceUser.Id, out var libItems)
                                || !libItems.TryGetValue(library.Id, out var items)
                                || items.Count == 0)
                                continue;

                            if (scope.History)
                            {
                                var stateItems = items
                                    .Where(i => matches.ContainsKey(i.Id) && (i.PlayCount > 0 || i.IsCompleted))
                                    .Select(i => new BulkUpsertMediaStatesRequest.MediaStateItem
                                    {
                                        MediaId = matches[i.Id],
                                        PlayCount = i.PlayCount,
                                        LastPlaybackPosition = i.LastPlaybackPosition ?? 0,
                                        ProgressPercentage = i.IsCompleted ? 100 : 0,
                                        IsCompleted = i.IsCompleted,
                                        LastInteractedAt = i.LastPlayedAt
                                    })
                                    .ToList();

                                if (stateItems.Count > 0)
                                {
                                    ctx.Status($"Importing {stateItems.Count} watch states for {sourceUser.Name}...");
                                    totalResult.ImportedWatchStates += await k7Client.BulkUpsertMediaStatesAsync(k7UserId, stateItems, strategy, cancellationToken);
                                }

                                var sessionItems = items
                                    .Where(i => matches.ContainsKey(i.Id) && i.PlayHistory.Count > 0)
                                    .SelectMany(i => i.PlayHistory.Select(p => new BulkCreatePlaybackSessionsRequest.PlaybackSessionItem
                                    {
                                        MediaId = matches[i.Id],
                                        StartedAt = p.PlayedAt.AddSeconds(-p.DurationSeconds),
                                        DurationSeconds = p.DurationSeconds,
                                        WatchedDurationSeconds = p.DurationSeconds,
                                        IsCompleted = true,
                                        IsTranscode = p.IsTranscode,
                                        VideoDecision = p.VideoDecision,
                                        AudioDecision = p.AudioDecision,
                                        Bitrate = p.Bitrate,
                                        SourceVideoCodec = p.SourceVideoCodec,
                                        SourceAudioCodec = p.SourceAudioCodec,
                                        SourceVideoWidth = p.SourceVideoWidth,
                                        SourceVideoHeight = p.SourceVideoHeight,
                                        StreamVideoCodec = p.StreamVideoCodec,
                                        StreamAudioCodec = p.StreamAudioCodec
                                    }))
                                    .ToList();

                                if (sessionItems.Count > 0)
                                {
                                    ctx.Status($"Importing {sessionItems.Count} playback sessions for {sourceUser.Name}...");
                                    totalResult.ImportedPlaybackSessions += await k7Client.BulkCreatePlaybackSessionsAsync(k7UserId, sessionItems, cancellationToken);
                                }
                            }

                            if (scope.Ratings)
                            {
                                var ratingItems = items
                                    .Where(i => matches.ContainsKey(i.Id) && i.Rating is > 0)
                                    .Select(i => new BulkUpsertRatingsRequest.RatingItem
                                    {
                                        MediaId = matches[i.Id],
                                        Value = i.Rating!.Value
                                    })
                                    .ToList();

                                if (ratingItems.Count > 0)
                                {
                                    ctx.Status($"Importing {ratingItems.Count} ratings for {sourceUser.Name}...");
                                    totalResult.ImportedRatings += await k7Client.BulkUpsertRatingsAsync(k7UserId, ratingItems, strategy, cancellationToken);
                                }
                            }
                        }
                    }

                    if (scope.Playlists)
                    {
                        ctx.Status("Fetching playlists...");
                        var playlists = await sourceClient.GetPlaylistsAsync(sourceUser.Id, cancellationToken);

                        if (dryRun) return;

                        foreach (var playlist in playlists)
                        {
                            var playlistMatches = await matcher.MatchPlaylistItemsAsync(playlist.Items, cancellationToken);
                            if (playlistMatches.Count == 0) continue;

                            ctx.Status($"Creating playlist '{playlist.Title}'...");
                            var playlistId = await k7Client.CreatePlaylistAsync(playlist.Title, cancellationToken);

                            foreach (var item in playlist.Items)
                            {
                                if (playlistMatches.TryGetValue(item.Id, out var mediaId))
                                    await k7Client.AddPlaylistItemAsync(playlistId, mediaId, cancellationToken);
                            }

                            totalResult.ImportedPlaylists++;
                        }
                    }
                });
        }

        // 9. Summary
        AnsiConsole.WriteLine();
        var table = new Table();
        table.AddColumn("Metric");
        table.AddColumn("Count");
        table.AddRow("Matched items", totalResult.MatchedItems.ToString());
        table.AddRow("Unmatched items", totalResult.UnmatchedItems.ToString());
        table.AddRow("Created media", totalResult.CreatedMedias.ToString());
        table.AddRow("Imported watch states", totalResult.ImportedWatchStates.ToString());
        table.AddRow("Imported playback sessions", totalResult.ImportedPlaybackSessions.ToString());
        table.AddRow("Imported ratings", totalResult.ImportedRatings.ToString());
        table.AddRow("Imported playlists", totalResult.ImportedPlaylists.ToString());
        AnsiConsole.Write(table);

        var skipped = new List<string>();
        if (!scope.History) skipped.Add("history");
        if (!scope.Ratings) skipped.Add("ratings");
        if (!scope.Playlists) skipped.Add("playlists");
        if (skipped.Count > 0)
            AnsiConsole.MarkupLine($"[dim]Skipped: {string.Join(", ", skipped)}[/]");

        if (totalResult.UnmatchedTitles.Count > 0)
        {
            AnsiConsole.MarkupLine($"\n[yellow]Unmatched titles ({totalResult.UnmatchedTitles.Count}):[/]");
            foreach (var title in totalResult.UnmatchedTitles.Take(50))
            {
                AnsiConsole.MarkupLine($"  [dim]- {Markup.Escape(title)}[/]");
            }
            if (totalResult.UnmatchedTitles.Count > 50)
            {
                AnsiConsole.MarkupLine($"  [dim]...and {totalResult.UnmatchedTitles.Count - 50} more[/]");
            }
        }

        if (dryRun)
        {
            AnsiConsole.MarkupLine("\n[yellow bold]DRY RUN — no changes were applied.[/]");
        }
    }

    private static Dictionary<string, string> ParseUserMappings(string[] mappings)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in mappings)
        {
            var parts = mapping.Split(':', 2);
            if (parts.Length == 2)
            {
                result[parts[0].Trim()] = parts[1].Trim();
            }
        }
        return result;
    }

    private static async Task<Dictionary<SourceUser, Guid>> ResolveUserMappingsAsync(
        List<SourceUser> sourceUsers,
        List<K7.Shared.Dtos.Users.UserDto> k7Users,
        Dictionary<string, string> explicitMappings,
        K7ApiClient k7Client,
        string sourceType,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<SourceUser, Guid>();

        if (sourceUsers.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No users found on source server.[/]");
            return result;
        }

        AnsiConsole.MarkupLine($"\n[bold]Source users ({sourceUsers.Count}):[/]");
        foreach (var user in sourceUsers)
        {
            AnsiConsole.MarkupLine($"  - {user.Name} (id: {user.Id})");
        }

        AnsiConsole.MarkupLine($"\n[bold]K7 users ({k7Users.Count}):[/]");
        foreach (var user in k7Users)
        {
            AnsiConsole.MarkupLine($"  - {user.UserName} (id: {user.Id})");
        }

        foreach (var sourceUser in sourceUsers)
        {
            if (explicitMappings.TryGetValue(sourceUser.Name, out var k7Username))
            {
                var k7User = k7Users.FirstOrDefault(u =>
                    string.Equals(u.UserName, k7Username, StringComparison.OrdinalIgnoreCase));

                if (k7User is not null)
                {
                    result[sourceUser] = k7User.Id;
                    AnsiConsole.MarkupLine($"[green]Mapped {sourceUser.Name} -> {k7User.UserName}[/]");
                }
                else
                {
                    AnsiConsole.MarkupLine($"[yellow]K7 user '{k7Username}' not found. Skipping {sourceUser.Name}.[/]");
                }
            }
            else
            {
                // Create temp user
                var tempUsername = $"{sourceType}-{sourceUser.Name.ToLowerInvariant().Replace(' ', '-')}";
                AnsiConsole.MarkupLine($"[dim]No mapping for {sourceUser.Name}, will use temp user '{tempUsername}'[/]");

                if (!dryRun)
                {
                    var existing = k7Users.FirstOrDefault(u =>
                        string.Equals(u.UserName, tempUsername, StringComparison.OrdinalIgnoreCase));

                    if (existing is not null)
                    {
                        result[sourceUser] = existing.Id;
                    }
                    else
                    {
                        var created = await k7Client.CreateUserAsync(tempUsername, "User", cancellationToken);
                        result[sourceUser] = created.Id;
                        AnsiConsole.MarkupLine($"[green]Created temp K7 user '{tempUsername}'[/]");
                    }
                }
            }
        }

        return result;
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

    private sealed record ImportScope(bool History, bool Ratings, bool Playlists);
}
