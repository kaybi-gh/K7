using K7.Server.Application.Common.Interfaces;
using K7.Shared.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.Services;

public interface IServerDiskMetricsProvider
{
    IReadOnlyList<ServerDiskVolumeDto> GetVolumes();
}

public sealed class ServerDiskMetricsProvider(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory) : IServerDiskMetricsProvider
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);
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
        var drives = new Dictionary<string, DriveInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in GetMonitoredPaths())
        {
            try
            {
                var drive = GetDriveForPath(path);
                if (drive is null || !drive.IsReady || drive.TotalSize <= 0)
                    continue;

                drives.TryAdd(drive.Name, drive);
            }
            catch
            {
            }
        }

        return drives.Values
            .Select(ToVolumeDto)
            .OrderBy(v => v.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static ServerDiskVolumeDto ToVolumeDto(DriveInfo drive)
    {
        var totalGb = drive.TotalSize / (1024d * 1024 * 1024);
        var freeGb = drive.AvailableFreeSpace / (1024d * 1024 * 1024);
        var usedGb = totalGb - freeGb;
        var freePercent = totalGb > 0 ? freeGb / totalGb * 100 : 0;
        var root = drive.Name.TrimEnd('\\', '/');
        var label = string.IsNullOrWhiteSpace(drive.VolumeLabel)
            ? root
            : $"{drive.VolumeLabel} ({root})";

        return new ServerDiskVolumeDto
        {
            Label = label,
            UsedGb = Math.Round(usedGb, 1),
            TotalGb = Math.Round(totalGb, 1),
            FreePercent = Math.Round(freePercent, 1)
        };
    }

    private HashSet<string> GetMonitoredPaths()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddPath(paths, AppContext.BaseDirectory);
        AddConfigPath(paths, "Paths:Config");
        AddConfigPath(paths, "Paths:Metadatas");
        AddConfigPath(paths, "Paths:Logs");
        AddConfigPath(paths, "Paths:Transcoding");

        try
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
            var libraryPaths = db.Libraries.AsNoTracking()
                .Where(l => l.RootPath != null && l.RootPath != "")
                .Select(l => l.RootPath!)
                .ToList();

            foreach (var libraryPath in libraryPaths)
                AddPath(paths, libraryPath);
        }
        catch
        {
        }

        return paths;
    }

    private void AddConfigPath(HashSet<string> paths, string key)
    {
        var value = configuration[key];
        if (!string.IsNullOrWhiteSpace(value))
            AddPath(paths, value);
    }

    private static void AddPath(HashSet<string> paths, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            paths.Add(Path.GetFullPath(path));
        }
        catch
        {
        }
    }

    private static DriveInfo? GetDriveForPath(string path)
    {
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
            return null;

        return new DriveInfo(root);
    }
}
