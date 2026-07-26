using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryPaths;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public sealed class LibraryFolderWatcherService(
    IServiceScopeFactory scopeFactory,
    ILogger<LibraryFolderWatcherService> logger) : BackgroundService, ILibraryFolderWatcher
{
    private static readonly TimeSpan ReloadInterval = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(45);

    private readonly Lock _sync = new();
    private readonly Dictionary<Guid, WatchedLibrary> _watchedLibraries = [];
    private readonly Dictionary<Guid, PendingScan> _pendingScans = [];
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("LibraryFolderWatcherService started");
        _stoppingToken = stoppingToken;

        await ReloadWatchersAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(ReloadInterval, stoppingToken);
                await ReloadWatchersAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error reloading library folder watchers");
            }
        }

        StopAllWatchers();
        logger.LogInformation("LibraryFolderWatcherService stopped");
    }

    public Task RefreshWatchersAsync(CancellationToken cancellationToken = default)
        => ReloadWatchersAsync(cancellationToken);

    private async Task ReloadWatchersAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();

        var libraries = await context.Libraries
            .AsNoTracking()
            .Where(l => l.RootPath != null && l.PeerServerId == null && l.RealtimeMonitorEnabled)
            .ToListAsync(cancellationToken);

        var desiredIds = libraries.Select(l => l.Id).ToHashSet();

        lock (_sync)
        {
            foreach (var libraryId in _watchedLibraries.Keys.Except(desiredIds).ToList())
            {
                StopWatcher(libraryId);
            }

            foreach (var library in libraries)
            {
                if (_watchedLibraries.ContainsKey(library.Id))
                    continue;

                StartWatcher(library);
            }
        }
    }

    private void StartWatcher(Library library)
    {
        if (library.RootPath is null || !Directory.Exists(library.RootPath))
        {
            logger.LogInformation("Skipping realtime monitor for library {LibraryId}: root path unavailable", library.Id);
            return;
        }

        var watched = new WatchedLibrary(library.Id, library.RootPath);

        try
        {
            // Per-directory watches (no IncludeSubdirectories) so Synology @eaDir and other
            // excluded NAS folders are never registered with inotify / ReadDirectoryChanges.
            var stack = new Stack<string>();
            stack.Push(library.RootPath);

            while (stack.Count > 0)
            {
                var currentDir = stack.Pop();
                if (!TryAddDirectoryWatch(watched, currentDir))
                    continue;

                IEnumerable<string> subDirs;
                try
                {
                    subDirs = Directory.EnumerateDirectories(currentDir);
                }
                catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
                {
                    logger.LogDebug(ex, "Skipping inaccessible directory while building watches for library {LibraryId}: {Path}", library.Id, currentDir);
                    continue;
                }

                foreach (var subDir in subDirs)
                {
                    var dirName = Path.GetFileName(subDir);
                    if (FileInfoHelper.IsExcludedDirectoryName(dirName))
                        continue;

                    stack.Push(subDir);
                }
            }

            if (watched.Watchers.Count == 0)
            {
                logger.LogWarning("Realtime monitor for library {LibraryId} created no watches at {RootPath}", library.Id, library.RootPath);
                watched.Dispose();
                return;
            }

            _watchedLibraries[library.Id] = watched;
            logger.LogInformation(
                "Started realtime monitor for library {LibraryId} at {RootPath} ({WatchCount} directory watches)",
                library.Id, library.RootPath, watched.Watchers.Count);
        }
        catch (Exception ex)
        {
            watched.Dispose();
            logger.LogWarning(ex, "Failed to start realtime monitor for library {LibraryId}", library.Id);
        }
    }

    private bool TryAddDirectoryWatch(WatchedLibrary watched, string directoryPath)
    {
        if (FileInfoHelper.IsExcludedPath(directoryPath))
            return false;

        if (watched.Watchers.ContainsKey(directoryPath))
            return true;

        try
        {
            var watcher = new FileSystemWatcher(directoryPath)
            {
                IncludeSubdirectories = false,
                InternalBufferSize = 65536,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
            };

            var libraryId = watched.LibraryId;
            watcher.Created += (_, e) => OnCreated(libraryId, e.FullPath);
            watcher.Changed += (_, e) => OnFileSystemEvent(libraryId, e.FullPath);
            watcher.Deleted += (_, e) => OnDeleted(libraryId, e.FullPath);
            watcher.Renamed += (_, e) => OnRenamed(libraryId, e.OldFullPath, e.FullPath);
            watcher.Error += (_, e) =>
            {
                var ex = e.GetException();
                if (ex is UnauthorizedAccessException)
                {
                    logger.LogDebug(ex, "FileSystemWatcher access denied for library {LibraryId} at {Path}", libraryId, directoryPath);
                    return;
                }

                logger.LogWarning(ex, "FileSystemWatcher error for library {LibraryId} at {Path}", libraryId, directoryPath);
            };
            watcher.EnableRaisingEvents = true;

            watched.Watchers[directoryPath] = watcher;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            logger.LogDebug(ex, "Could not watch directory for library {LibraryId}: {Path}", watched.LibraryId, directoryPath);
            return false;
        }
    }

    private void OnCreated(Guid libraryId, string path)
    {
        if (FileInfoHelper.IsExcludedPath(path))
            return;

        if (Directory.Exists(path))
        {
            lock (_sync)
            {
                if (_watchedLibraries.TryGetValue(libraryId, out var watched))
                    TryAddDirectoryWatch(watched, path);
            }
        }

        OnFileSystemEvent(libraryId, path);
    }

    private void OnDeleted(Guid libraryId, string path)
    {
        lock (_sync)
        {
            if (_watchedLibraries.TryGetValue(libraryId, out var watched)
                && watched.Watchers.Remove(path, out var watcher))
            {
                watcher.Dispose();
            }
        }

        OnFileSystemEvent(libraryId, path);
    }

    private void OnRenamed(Guid libraryId, string oldPath, string newPath)
    {
        lock (_sync)
        {
            if (_watchedLibraries.TryGetValue(libraryId, out var watched)
                && watched.Watchers.Remove(oldPath, out var watcher))
            {
                watcher.Dispose();
            }

            if (!FileInfoHelper.IsExcludedPath(newPath) && Directory.Exists(newPath) && watched is not null)
                TryAddDirectoryWatch(watched, newPath);
        }

        OnFileSystemEvent(libraryId, oldPath);
        OnFileSystemEvent(libraryId, newPath);
    }

    private void OnFileSystemEvent(Guid libraryId, string path)
    {
        if (FileInfoHelper.IsExcludedPath(path))
            return;

        lock (_sync)
        {
            if (!_pendingScans.TryGetValue(libraryId, out var pending))
            {
                pending = new PendingScan();
                _pendingScans[libraryId] = pending;
            }

            pending.Paths.Add(path);
            pending.DebounceTimer?.Dispose();
            pending.DebounceTimer = new Timer(_ => _ = FlushPendingScanAsync(libraryId), null, DebounceDelay, Timeout.InfiniteTimeSpan);
        }
    }

    private async Task FlushPendingScanAsync(Guid libraryId)
    {
        List<string> paths;

        lock (_sync)
        {
            if (!_pendingScans.TryGetValue(libraryId, out var pending))
                return;

            paths = pending.Paths.Where(path => !FileInfoHelper.IsExcludedPath(path)).ToList();
            pending.Paths.Clear();
            pending.DebounceTimer?.Dispose();
            pending.DebounceTimer = null;
            _pendingScans.Remove(libraryId);
        }

        if (paths.Count == 0)
            return;

        if (_stoppingToken.IsCancellationRequested)
            return;

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var sender = scope.ServiceProvider.GetRequiredService<ISender>();

            await sender.Send(new CreateBackgroundTaskCommand
            {
                Request = new IndexLibraryPathsCommand(libraryId, paths),
                Priority = BackgroundTaskPriority.Normal,
                TargetEntityId = libraryId,
                TargetEntityTypeName = nameof(Library),
                MaxAttempts = 1,
                TimeoutSeconds = 3600,
                ConcurrencyGroup = "library-scan"
            }, _stoppingToken);
        }
        catch (OperationCanceledException) when (_stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Path scan queue canceled for library {LibraryId}: service is stopping", libraryId);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to queue path scan for library {LibraryId}", libraryId);
        }
    }

    private void StopWatcher(Guid libraryId)
    {
        if (_watchedLibraries.Remove(libraryId, out var watched))
        {
            watched.Dispose();
            logger.LogInformation("Stopped realtime monitor for library {LibraryId}", libraryId);
        }

        if (_pendingScans.Remove(libraryId, out var pending))
        {
            pending.DebounceTimer?.Dispose();
        }
    }

    private void StopAllWatchers()
    {
        lock (_sync)
        {
            foreach (var libraryId in _watchedLibraries.Keys.ToList())
            {
                StopWatcher(libraryId);
            }
        }
    }

    public override void Dispose()
    {
        StopAllWatchers();
        base.Dispose();
    }

    private sealed class WatchedLibrary(Guid libraryId, string rootPath)
    {
        public Guid LibraryId { get; } = libraryId;
        public string RootPath { get; } = rootPath;
        public Dictionary<string, FileSystemWatcher> Watchers { get; } = new(StringComparer.OrdinalIgnoreCase);

        public void Dispose()
        {
            foreach (var watcher in Watchers.Values)
                watcher.Dispose();

            Watchers.Clear();
        }
    }

    private sealed class PendingScan
    {
        public HashSet<string> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Timer? DebounceTimer;
    }
}

public interface ILibraryFolderWatcher
{
    Task RefreshWatchersAsync(CancellationToken cancellationToken = default);
}
