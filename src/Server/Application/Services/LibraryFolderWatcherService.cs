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
    /// <summary>
    /// Libraries whose root could not be watched (often NFS/CIFS without inotify). Tracked so the
    /// 5-minute reload does not spam Warning; still retried in case the mount starts supporting watches.
    /// </summary>
    private readonly Dictionary<Guid, FailedWatchState> _failedWatchLibraries = [];
    private CancellationToken _stoppingToken;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SetupCompletionGate.WaitUntilCompletedAsync(scopeFactory, logger, stoppingToken);
        if (stoppingToken.IsCancellationRequested)
            return;

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

            foreach (var libraryId in _failedWatchLibraries.Keys.Except(desiredIds).ToList())
                _failedWatchLibraries.Remove(libraryId);
        }

        foreach (var library in libraries)
        {
            lock (_sync)
            {
                if (_watchedLibraries.ContainsKey(library.Id))
                    continue;
            }

            StartWatcher(library);
        }
    }

    private void StartWatcher(Library library)
    {
        if (library.RootPath is null || !Directory.Exists(library.RootPath))
        {
            logger.LogInformation("Skipping realtime monitor for library {LibraryId}: root path unavailable", library.Id);
            lock (_sync)
                _failedWatchLibraries.Remove(library.Id);
            return;
        }

        var watched = new WatchedLibrary(library.Id, library.RootPath);

        try
        {
            // One FileSystemWatcher per library (IncludeSubdirectories). Linux FileSystemWatcher
            // uses one inotify instance each; a watcher per directory hits max_user_instances
            // (default 128) after a single medium-sized tree. Excluded NAS folders (@eaDir, etc.)
            // may still consume watches; events from those paths are ignored.
            if (!TryStartRecursiveWatch(watched, out var watchError))
            {
                watched.Dispose();
                lock (_sync)
                    RecordFailedWatch(library.Id, library.RootPath, watchError);
                return;
            }

            lock (_sync)
            {
                if (_watchedLibraries.ContainsKey(library.Id))
                {
                    watched.Dispose();
                    return;
                }

                _failedWatchLibraries.Remove(library.Id);
                _watchedLibraries[library.Id] = watched;
            }

            logger.LogInformation(
                "Started realtime monitor for library {LibraryId} at {RootPath}",
                library.Id, library.RootPath);
        }
        catch (Exception ex)
        {
            watched.Dispose();
            logger.LogWarning(ex, "Failed to start realtime monitor for library {LibraryId}", library.Id);
            lock (_sync)
                _failedWatchLibraries[library.Id] = new FailedWatchState(library.RootPath, ex.Message);
        }
    }

    private void RecordFailedWatch(Guid libraryId, string rootPath, Exception? error)
    {
        var previous = _failedWatchLibraries.GetValueOrDefault(libraryId);
        var sameFailure = previous is not null
            && string.Equals(previous.RootPath, rootPath, StringComparison.OrdinalIgnoreCase)
            && string.Equals(previous.ErrorMessage, error?.Message, StringComparison.Ordinal);

        if (!sameFailure)
        {
            logger.LogWarning(
                error,
                "Realtime monitor for library {LibraryId} created no watches at {RootPath}. Common causes: filesystem without inotify (NFS/CIFS), permissions, or inotify limits. Disable realtime monitoring or rely on AutoScanIntervalHours",
                libraryId,
                rootPath);
        }
        else
        {
            logger.LogDebug(
                error,
                "Realtime monitor for library {LibraryId} still has no watches at {RootPath}",
                libraryId,
                rootPath);
        }

        _failedWatchLibraries[libraryId] = new FailedWatchState(rootPath, error?.Message);
    }

    private bool TryStartRecursiveWatch(WatchedLibrary watched, out Exception? error)
    {
        error = null;

        try
        {
            var watcher = new FileSystemWatcher(watched.RootPath)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 65536,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
            };

            var libraryId = watched.LibraryId;
            watcher.Created += (_, e) => OnFileSystemEvent(libraryId, e.FullPath);
            watcher.Changed += (_, e) => OnFileSystemEvent(libraryId, e.FullPath);
            watcher.Deleted += (_, e) => OnFileSystemEvent(libraryId, e.FullPath);
            watcher.Renamed += (_, e) =>
            {
                OnFileSystemEvent(libraryId, e.OldFullPath);
                OnFileSystemEvent(libraryId, e.FullPath);
            };
            watcher.Error += (_, e) =>
            {
                var ex = e.GetException();
                if (ex is UnauthorizedAccessException)
                {
                    logger.LogDebug(ex, "FileSystemWatcher access denied for library {LibraryId} at {Path}", libraryId, watched.RootPath);
                    return;
                }

                logger.LogWarning(ex, "FileSystemWatcher error for library {LibraryId} at {Path}", libraryId, watched.RootPath);
            };
            watcher.EnableRaisingEvents = true;

            watched.Watcher = watcher;
            return true;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException or ArgumentException)
        {
            error = ex;
            logger.LogDebug(ex, "Could not watch library {LibraryId} at {Path}", watched.LibraryId, watched.RootPath);
            return false;
        }
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
                TargetEntityId = libraryId,
                TargetEntityTypeName = nameof(Library),
                Lane = BackgroundTaskLane.LibraryScan,
                WorkClass = BackgroundTaskWorkClass.CriticalLink,
                TriggeredBy = BackgroundTaskTriggeredBy.Watcher,
                MaxAttempts = 1,
                TimeoutSeconds = 3600
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
        public FileSystemWatcher? Watcher { get; set; }

        public void Dispose()
        {
            Watcher?.Dispose();
            Watcher = null;
        }
    }

    private sealed class PendingScan
    {
        public HashSet<string> Paths { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Timer? DebounceTimer;
    }

    private sealed record FailedWatchState(string RootPath, string? ErrorMessage);
}

public interface ILibraryFolderWatcher
{
    Task RefreshWatchersAsync(CancellationToken cancellationToken = default);
}
