using System.Text;
using K7.Import.Matching;
using K7.Import.Models;
using Spectre.Console;

namespace K7.Import.Commands;

internal static class ImportReportPrinter
{
    private const int ConsoleMediaListLimit = 30;

    public static void Print(ImportReport report, ImportScope scope, string? reportPath = null)
    {
        var plain = BuildPlainText(report, scope);

        if (!string.IsNullOrWhiteSpace(reportPath))
        {
            var fullPath = Path.GetFullPath(reportPath);
            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(fullPath, plain, Encoding.UTF8);
            AnsiConsole.MarkupLine($"\n[green]Full report written to[/] {Markup.Escape(fullPath)}");
            PrintConsoleSummary(report, scope, truncatedMediaLists: true);
            AnsiConsole.MarkupLine($"[dim]Full matched / would-create / unmatched media lists are in the report file.[/]");
        }
        else
        {
            PrintConsoleSummary(report, scope, truncatedMediaLists: false);
        }

        if (report.DryRun)
            AnsiConsole.MarkupLine("\n[yellow bold]DRY RUN - no changes were applied.[/]");
    }

    public static string BuildPlainText(ImportReport report, ImportScope scope)
    {
        var sb = new StringBuilder();
        sb.AppendLine(report.DryRun ? "DRY RUN report" : "Import report");
        sb.AppendLine(new string('=', 72));
        sb.AppendLine($"Generated: {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss zzz}");
        sb.AppendLine();

        AppendWarnings(sb, report);
        AppendUsers(sb, report);
        AppendMediaSummary(sb, report);
        AppendPerUser(sb, report, scope);
        AppendPlaylists(sb, report, scope);
        AppendMediaLists(sb, report);

        if (report.DryRun)
        {
            sb.AppendLine();
            sb.AppendLine("DRY RUN - no changes were applied.");
        }

        return sb.ToString();
    }

    private static void PrintConsoleSummary(ImportReport report, ImportScope scope, bool truncatedMediaLists)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine(report.DryRun
            ? "[yellow bold]DRY RUN report[/]"
            : "[bold]Import report[/]");

        PrintWarnings(report);
        PrintUsers(report);
        PrintMediaSummary(report);
        PrintPerUser(report, scope);
        PrintPlaylistDetails(report, scope);
        PrintMediaLists(report, truncatedMediaLists ? ConsoleMediaListLimit : null);
    }

    private static void PrintUsers(ImportReport report)
    {
        AnsiConsole.MarkupLine("\n[bold underline]Users[/]");
        var table = new Table().AddColumn("Source").AddColumn("Target K7").AddColumn("Plan");
        foreach (var user in report.Users)
        {
            var plan = user.Kind switch
            {
                UserMappingKind.MappedExisting => "[green]mapped[/]",
                UserMappingKind.AutoMapped => "[green]auto-mapped[/]",
                UserMappingKind.ReuseTemp => "[cyan]reuse temp[/]",
                UserMappingKind.WouldCreateTemp => "[yellow]would create temp[/]",
                UserMappingKind.CreatedTemp => "[green]created temp[/]",
                UserMappingKind.Skipped => $"[red]skipped[/] ({Markup.Escape(user.SkipReason ?? "")})",
                _ => user.Kind.ToString()
            };
            table.AddRow(
                Markup.Escape(user.Source.DisplayName),
                Markup.Escape(user.TargetUsername),
                plan);
        }

        AnsiConsole.Write(table);

        var mapped = report.Users.Count(u => u.Kind is UserMappingKind.MappedExisting or UserMappingKind.AutoMapped);
        var temps = report.Users.Count(u => u.Kind is UserMappingKind.WouldCreateTemp
            or UserMappingKind.CreatedTemp or UserMappingKind.ReuseTemp);
        var skipped = report.Users.Count(u => u.Kind is UserMappingKind.Skipped);
        AnsiConsole.MarkupLine(
            $"[dim]Summary: {mapped} mapped, {temps} temp account(s), {skipped} skipped[/]");
    }

    private static void PrintMediaSummary(ImportReport report)
    {
        AnsiConsole.MarkupLine("\n[bold underline]Media matching[/]");
        var table = new Table().AddColumn("Metric").AddColumn("Count");
        table.AddRow("Matched existing", report.MatchedExisting.ToString());
        table.AddRow("  via external ID", report.MatchedByExternalId.ToString());
        table.AddRow("  via file path", report.MatchedByPath.ToString());
        table.AddRow("  via title/identity", report.MatchedByTitle.ToString());
        if (report.DryRun)
            table.AddRow("Would create virtual", report.WouldCreateMedias.ToString());
        else
            table.AddRow("Created virtual", report.CreatedMedias.ToString());
        table.AddRow("Unmatched", report.UnmatchedMedias.ToString());
        AnsiConsole.Write(table);
    }

    private static void PrintPerUser(ImportReport report, ImportScope scope)
    {
        AnsiConsole.MarkupLine("\n[bold underline]Per user[/]");
        foreach (var user in report.PerUser)
        {
            var plan = user.Plan;
            AnsiConsole.MarkupLine(
                $"\n[bold]{Markup.Escape(plan.Source.DisplayName)}[/] -> {Markup.Escape(plan.TargetUsername)} " +
                $"([dim]{plan.Kind}[/])");

            if (scope.History)
            {
                AnsiConsole.MarkupLine(
                    $"  History medias: {user.HistorySourceItems} source | " +
                    $"{user.HistoryMatched} matched | " +
                    $"{(report.DryRun ? user.HistoryWouldCreate : user.HistoryCreated)} " +
                    $"{(report.DryRun ? "would create" : "created")} | " +
                    $"{user.HistoryUnmatched} unmatched");
                AnsiConsole.MarkupLine(
                    $"  History writes: {user.WatchStates} watch state(s), {user.PlaybackSessions} session(s)");
            }

            if (scope.Ratings)
            {
                AnsiConsole.MarkupLine(
                    $"  Ratings medias: {user.RatingsSourceItems} source | " +
                    $"{user.RatingsMatched} matched | " +
                    $"{(report.DryRun ? user.RatingsWouldCreate : user.RatingsCreated)} " +
                    $"{(report.DryRun ? "would create" : "created")} | " +
                    $"{user.RatingsUnmatched} unmatched");
                AnsiConsole.MarkupLine($"  Ratings writes: {user.RatingsToImport} rating(s)");
            }

            if (scope.Playlists)
            {
                var active = user.Playlists.Count(p => !p.Skipped);
                var skipped = user.Playlists.Count(p => p.Skipped) + user.SkippedDynamicPlaylists;
                AnsiConsole.MarkupLine(
                    $"  Playlists: {active} to import, {skipped} skipped/ignored");
            }
        }
    }

    private static void PrintPlaylistDetails(ImportReport report, ImportScope scope)
    {
        if (!scope.Playlists)
            return;

        var any = report.PerUser.SelectMany(u => u.Playlists).Any()
            || report.PerUser.Any(u => u.SkippedDynamicPlaylists > 0);
        if (!any)
        {
            AnsiConsole.MarkupLine("\n[bold underline]Playlists[/]");
            AnsiConsole.MarkupLine("[dim]No playlists found on source.[/]");
            return;
        }

        AnsiConsole.MarkupLine("\n[bold underline]Playlists[/]");
        var table = new Table()
            .AddColumn("User")
            .AddColumn("Playlist")
            .AddColumn("Status")
            .AddColumn("Source")
            .AddColumn("Matched")
            .AddColumn(report.DryRun ? "Would create" : "Created")
            .AddColumn("Unmatched");

        foreach (var user in report.PerUser)
        {
            foreach (var playlist in user.Playlists)
            {
                var status = playlist.Skipped
                    ? $"[yellow]skipped[/] ({Markup.Escape(playlist.SkipReason ?? "")})"
                    : report.DryRun ? "[cyan]would import[/]" : "[green]imported[/]";
                table.AddRow(
                    Markup.Escape(user.Plan.Source.Name),
                    Markup.Escape(playlist.Title),
                    status,
                    playlist.SourceItems.ToString(),
                    playlist.Matched.ToString(),
                    (report.DryRun ? playlist.WouldCreate : playlist.Created).ToString(),
                    playlist.Unmatched.ToString());
            }
        }

        AnsiConsole.Write(table);
    }

    private static void PrintMediaLists(ImportReport report, int? limit)
    {
        WriteMediaSection("Matched existing media", OrderMediaResults(report.MediaBySourceId.Values
            .Where(r => r.Status is MatchStatus.MatchedByExternalId
                or MatchStatus.MatchedByPath
                or MatchStatus.MatchedByTitle)), limit);

        WriteMediaSection(
            report.DryRun ? "Would create virtual media" : "Created virtual media",
            OrderMediaResults(MusicItemCollapser.DistinctCreates(report.MediaBySourceId.Values
                .Where(r => r.Status is MatchStatus.WouldCreate or MatchStatus.Created))), limit);

        WriteMediaSection("Unmatched media", OrderMediaResults(report.MediaBySourceId.Values
            .Where(r => r.Status is MatchStatus.Unmatched)), limit);
    }

    private static void WriteMediaSection(string heading, IEnumerable<ItemMatchResult> results, int? limit)
    {
        var list = results.ToList();
        AnsiConsole.MarkupLine($"\n[bold underline]{Markup.Escape(heading)} ({list.Count})[/]");
        if (list.Count == 0)
        {
            AnsiConsole.MarkupLine("[dim](none)[/]");
            return;
        }

        var shown = limit is null ? list : list.Take(limit.Value).ToList();
        foreach (var result in shown)
            AnsiConsole.MarkupLine($"  [dim]- {Markup.Escape(FormatMediaDetail(result))}[/]");

        if (limit is not null && list.Count > limit.Value)
            AnsiConsole.MarkupLine($"  [dim]...and {list.Count - limit.Value} more (see --report file)[/]");
    }

    private static void PrintWarnings(ImportReport report)
    {
        if (report.Warnings.Count == 0)
            return;

        AnsiConsole.MarkupLine("\n[bold underline]Warnings[/]");
        foreach (var warning in report.Warnings)
            AnsiConsole.MarkupLine($"[yellow]{Markup.Escape(warning)}[/]");
    }

    private static void AppendWarnings(StringBuilder sb, ImportReport report)
    {
        if (report.Warnings.Count == 0)
            return;

        sb.AppendLine("Warnings");
        sb.AppendLine(new string('-', 72));
        foreach (var warning in report.Warnings)
            sb.AppendLine($"- {warning}");
        sb.AppendLine();
    }

    private static void AppendUsers(StringBuilder sb, ImportReport report)
    {
        sb.AppendLine("Users");
        sb.AppendLine(new string('-', 72));
        foreach (var user in report.Users)
        {
            var plan = user.Kind switch
            {
                UserMappingKind.Skipped => $"skipped ({user.SkipReason})",
                _ => user.Kind.ToString()
            };
            sb.AppendLine($"- {user.Source.DisplayName} -> {user.TargetUsername} [{plan}] (source id: {user.Source.Id})");
        }

        var mapped = report.Users.Count(u => u.Kind is UserMappingKind.MappedExisting or UserMappingKind.AutoMapped);
        var temps = report.Users.Count(u => u.Kind is UserMappingKind.WouldCreateTemp
            or UserMappingKind.CreatedTemp or UserMappingKind.ReuseTemp);
        var skipped = report.Users.Count(u => u.Kind is UserMappingKind.Skipped);
        sb.AppendLine($"Summary: {mapped} mapped, {temps} temp account(s), {skipped} skipped");
        sb.AppendLine();
    }

    private static void AppendMediaSummary(StringBuilder sb, ImportReport report)
    {
        sb.AppendLine("Media matching");
        sb.AppendLine(new string('-', 72));
        sb.AppendLine($"Matched existing: {report.MatchedExisting}");
        sb.AppendLine($"  via external ID: {report.MatchedByExternalId}");
        sb.AppendLine($"  via file path: {report.MatchedByPath}");
        sb.AppendLine($"  via title/identity: {report.MatchedByTitle}");
        sb.AppendLine(report.DryRun
            ? $"Would create virtual: {report.WouldCreateMedias}"
            : $"Created virtual: {report.CreatedMedias}");
        sb.AppendLine($"Unmatched: {report.UnmatchedMedias}");
        sb.AppendLine();
    }

    private static void AppendPerUser(StringBuilder sb, ImportReport report, ImportScope scope)
    {
        sb.AppendLine("Per user");
        sb.AppendLine(new string('-', 72));
        foreach (var user in report.PerUser)
        {
            var plan = user.Plan;
            sb.AppendLine($"{plan.Source.DisplayName} -> {plan.TargetUsername} [{plan.Kind}]");

            if (scope.History)
            {
                sb.AppendLine(
                    $"  History medias: {user.HistorySourceItems} source | " +
                    $"{user.HistoryMatched} matched | " +
                    $"{(report.DryRun ? user.HistoryWouldCreate : user.HistoryCreated)} " +
                    $"{(report.DryRun ? "would create" : "created")} | " +
                    $"{user.HistoryUnmatched} unmatched");
                sb.AppendLine(
                    $"  History writes: {user.WatchStates} watch state(s), {user.PlaybackSessions} session(s)");
            }

            if (scope.Ratings)
            {
                sb.AppendLine(
                    $"  Ratings medias: {user.RatingsSourceItems} source | " +
                    $"{user.RatingsMatched} matched | " +
                    $"{(report.DryRun ? user.RatingsWouldCreate : user.RatingsCreated)} " +
                    $"{(report.DryRun ? "would create" : "created")} | " +
                    $"{user.RatingsUnmatched} unmatched");
                sb.AppendLine($"  Ratings writes: {user.RatingsToImport} rating(s)");
            }

            if (scope.Playlists)
            {
                var active = user.Playlists.Count(p => !p.Skipped);
                var skipped = user.Playlists.Count(p => p.Skipped) + user.SkippedDynamicPlaylists;
                sb.AppendLine($"  Playlists: {active} to import, {skipped} skipped/ignored");
            }

            sb.AppendLine();
        }
    }

    private static void AppendPlaylists(StringBuilder sb, ImportReport report, ImportScope scope)
    {
        if (!scope.Playlists)
            return;

        sb.AppendLine("Playlists");
        sb.AppendLine(new string('-', 72));

        var any = report.PerUser.SelectMany(u => u.Playlists).Any()
            || report.PerUser.Any(u => u.SkippedDynamicPlaylists > 0);
        if (!any)
        {
            sb.AppendLine("No playlists found on source.");
            sb.AppendLine();
            return;
        }

        foreach (var user in report.PerUser)
        {
            foreach (var playlist in user.Playlists)
            {
                var status = playlist.Skipped
                    ? $"skipped ({playlist.SkipReason})"
                    : report.DryRun ? "would import" : "imported";
                sb.AppendLine(
                    $"- [{user.Plan.Source.Name}] {playlist.Title}: {status}; " +
                    $"source={playlist.SourceItems}, matched={playlist.Matched}, " +
                    $"{(report.DryRun ? "would create" : "created")}=" +
                    $"{(report.DryRun ? playlist.WouldCreate : playlist.Created)}, " +
                    $"unmatched={playlist.Unmatched}");
            }
        }

        sb.AppendLine();
    }

    private static void AppendMediaLists(StringBuilder sb, ImportReport report)
    {
        AppendMediaSection(sb, "Matched existing media", OrderMediaResults(report.MediaBySourceId.Values
            .Where(r => r.Status is MatchStatus.MatchedByExternalId
                or MatchStatus.MatchedByPath
                or MatchStatus.MatchedByTitle)));

        AppendMediaSection(
            sb,
            report.DryRun ? "Would create virtual media" : "Created virtual media",
            OrderMediaResults(MusicItemCollapser.DistinctCreates(report.MediaBySourceId.Values
                .Where(r => r.Status is MatchStatus.WouldCreate or MatchStatus.Created))));

        AppendMediaSection(sb, "Unmatched media", OrderMediaResults(report.MediaBySourceId.Values
            .Where(r => r.Status is MatchStatus.Unmatched)));
    }

    private static void AppendMediaSection(StringBuilder sb, string heading, IEnumerable<ItemMatchResult> results)
    {
        var list = results.ToList();
        sb.AppendLine($"{heading} ({list.Count})");
        sb.AppendLine(new string('-', 72));
        if (list.Count == 0)
        {
            sb.AppendLine("(none)");
            sb.AppendLine();
            return;
        }

        foreach (var result in list)
            sb.AppendLine($"- {FormatMediaDetail(result)}");

        sb.AppendLine();
    }

    private static IEnumerable<ItemMatchResult> OrderMediaResults(IEnumerable<ItemMatchResult> results) =>
        results
            .OrderBy(r => MediaTypeSortKey(r.Item.MediaType))
            .ThenBy(r => MediaSortTitle(r.Item), StringComparer.OrdinalIgnoreCase);

    private static int MediaTypeSortKey(string? mediaType) => mediaType switch
    {
        "movie" => 0,
        "serie" => 1,
        "episode" => 2,
        "music" => 3,
        _ => 4
    };

    private static string MediaSortTitle(SourceMediaItem item)
    {
        if (item.MediaType is "episode" && !string.IsNullOrWhiteSpace(item.SeriesTitle))
        {
            var season = item.SeasonNumber?.ToString("00") ?? "??";
            var episode = item.EpisodeNumber?.ToString("00") ?? "??";
            return $"{item.SeriesTitle} S{season}E{episode} {item.Title}";
        }

        if (item.MediaType is "music" && !string.IsNullOrWhiteSpace(item.ArtistName))
            return $"{item.ArtistName} - {item.Title}";

        return item.Title;
    }

    internal static string FormatMediaDetail(ItemMatchResult result)
    {
        var item = result.Item;
        var bits = new List<string>
        {
            $"[{result.Status}]",
            FormatTitle(item),
            $"id={item.Id}"
        };

        if (result.MediaId is Guid mediaId)
            bits.Add($"k7={mediaId}");

        if (item.ProviderIds.Count > 0)
        {
            var ids = string.Join(", ", item.ProviderIds
                .OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase)
                .Select(k => $"{k.Key}:{k.Value}"));
            bits.Add($"providers=[{ids}]");
        }

        if (item.FilePaths.Count > 0)
            bits.Add($"paths=[{string.Join(" | ", item.FilePaths)}]");

        return string.Join(" ", bits);
    }

    internal static string FormatTitle(SourceMediaItem item)
    {
        var type = string.IsNullOrWhiteSpace(item.MediaType) ? "?" : item.MediaType;
        var year = item.Year?.ToString() ?? "?";
        if (item.MediaType is "episode" && !string.IsNullOrWhiteSpace(item.SeriesTitle))
        {
            var season = item.SeasonNumber?.ToString("00") ?? "??";
            var episode = item.EpisodeNumber?.ToString("00") ?? "??";
            return $"[{type}] {item.SeriesTitle} S{season}E{episode} - {item.Title} ({year})";
        }

        if (item.MediaType is "music" && !string.IsNullOrWhiteSpace(item.ArtistName))
            return $"[{type}] {item.ArtistName} - {item.Title} ({year})";

        return $"[{type}] {item.Title} ({year})";
    }
}

internal sealed record ImportScope(bool History, bool Ratings, bool Playlists);
