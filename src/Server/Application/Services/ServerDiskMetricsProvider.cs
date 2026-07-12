using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.Services;

public interface IServerDiskMetricsProvider
{
    IReadOnlyList<ServerDiskVolumeDto> GetVolumes();
}

public sealed class ServerDiskMetricsProvider(
    IOptions<PathsConfiguration> pathsOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<ServerDiskMetricsProvider> logger) : IServerDiskMetricsProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
    private readonly PathsConfiguration _paths = pathsOptions.Value;
    private readonly object _lock = new();
    private IReadOnlyList<ServerDiskVolumeDto> _cached = [];
    private DateTime _cachedAt = DateTime.MinValue;

    public IReadOnlyList<ServerDiskVolumeDto> GetVolumes()
    {
        lock (_lock)
        {
            if (DateTime.UtcNow - _cachedAt < CacheDuration)
                return _cached;

            _cached = ComputeVolumes();
            _cachedAt = DateTime.UtcNow;
            return _cached;
        }
    }

    private IReadOnlyList<ServerDiskVolumeDto> ComputeVolumes()
    {
        var monitoredPaths = GetMonitoredPaths();
        if (monitoredPaths.Count == 0)
            return [];

        var rootDriveKey = GetDriveKey(monitoredPaths[0].ResolvedPath);
        var volumesByDrive = new Dictionary<string, (DriveInfo Drive, List<string> Sources)>(StringComparer.OrdinalIgnoreCase);

        foreach (var (label, resolvedPath) in monitoredPaths)
        {
            try
            {
                var drive = GetDriveForPath(resolvedPath);
                if (drive is null || !drive.IsReady || drive.TotalSize <= 0)
                    continue;

                var driveKey = GetDriveKey(resolvedPath);
                if (driveKey is null)
                    continue;

                if (!volumesByDrive.TryGetValue(driveKey, out var entry))
                {
                    volumesByDrive[driveKey] = (drive, [label]);
                    continue;
                }

                if (!entry.Sources.Contains(label, StringComparer.OrdinalIgnoreCase))
                    entry.Sources.Add(label);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "Skipping monitored path {Label} at {Path}", label, resolvedPath);
            }
        }

        if (volumesByDrive.Count == 0)
            return [];

        var volumes = new List<ServerDiskVolumeDto>(volumesByDrive.Count);

        if (rootDriveKey is not null && volumesByDrive.TryGetValue(rootDriveKey, out var rootEntry))
            volumes.Add(ToVolumeDto(rootEntry.Drive, rootEntry.Sources, isRoot: true));

        foreach (var (driveKey, entry) in volumesByDrive.OrderBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (driveKey == rootDriveKey)
                continue;

            volumes.Add(ToVolumeDto(entry.Drive, entry.Sources, isRoot: false));
        }

        return volumes;
    }

    private List<MonitoredPath> GetMonitoredPaths()
    {
        var paths = new List<MonitoredPath>
        {
            new("Application", ResolvePath(AppContext.BaseDirectory))
        };

        AddConfiguredPath(paths, "Config", _paths.Config);
        AddConfiguredPath(paths, "Metadatas", _paths.Metadatas);
        AddConfiguredPath(paths, "Logs", _paths.Logs);
        AddConfiguredPath(paths, "Transcoding", _paths.Transcoding);

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var libraries = db.Libraries.AsNoTracking()
                .Where(l => l.RootPath != null && l.RootPath != "")
                .Select(l => new { l.Title, l.RootPath })
                .ToList();

            foreach (var library in libraries)
                AddConfiguredPath(paths, library.Title, library.RootPath!);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to load library paths for disk metrics");
        }

        return paths;
    }

    private static void AddConfiguredPath(List<MonitoredPath> paths, string label, string path)
    {
        var resolved = ResolvePath(path);
        if (string.IsNullOrWhiteSpace(resolved))
            return;

        if (paths.Any(p => string.Equals(p.ResolvedPath, resolved, StringComparison.OrdinalIgnoreCase)))
            return;

        paths.Add(new MonitoredPath(label, resolved));
    }

    private static string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        try
        {
            return Path.GetFullPath(path);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to resolve path {Path}", path);
            return path;
        }
    }

    private static ServerDiskVolumeDto ToVolumeDto(DriveInfo drive, IReadOnlyList<string> sources, bool isRoot)
    {
        var totalGb = drive.TotalSize / (1024d * 1024 * 1024);
        var freeGb = drive.AvailableFreeSpace / (1024d * 1024 * 1024);
        var usedGb = totalGb - freeGb;
        var freePercent = totalGb > 0 ? freeGb / totalGb * 100 : 0;
        var root = drive.Name.TrimEnd('\\', '/');

        return new ServerDiskVolumeDto
        {
            Label = BuildLabel(drive, sources, root, isRoot),
            UsedGb = Math.Round(usedGb, 1),
            TotalGb = Math.Round(totalGb, 1),
            FreePercent = Math.Round(freePercent, 1)
        };
    }

    private static string BuildLabel(DriveInfo drive, IReadOnlyList<string> sources, string root, bool isRoot)
    {
        if (isRoot && !string.IsNullOrWhiteSpace(drive.VolumeLabel))
            return $"{drive.VolumeLabel} ({root})";

        if (sources.Count == 1)
            return $"{sources[0]} ({root})";

        if (!string.IsNullOrWhiteSpace(drive.VolumeLabel))
            return $"{drive.VolumeLabel} ({root})";

        return root;
    }

    private static DriveInfo? GetDriveForPath(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
            return null;

        return new DriveInfo(root);
    }

    private static string? GetDriveKey(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
            return null;

        return root.TrimEnd('\\', '/');
    }

    private readonly record struct MonitoredPath(string Label, string ResolvedPath);
}
