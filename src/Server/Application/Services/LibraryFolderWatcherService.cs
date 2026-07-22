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

        try
        {
            var watcher = new FileSystemWatcher(library.RootPath)
            {
                IncludeSubdirectories = true,
                InternalBufferSize = 65536,
                NotifyFilter = NotifyFilters.FileName
                    | NotifyFilters.DirectoryName
                    | NotifyFilters.LastWrite
                    | NotifyFilters.Size
            };

            watcher.Created += (_, e) => OnFileSystemEvent(library.Id, e.FullPath);
            watcher.Changed += (_, e) => OnFileSystemEvent(library.Id, e.FullPath);
            watcher.Deleted += (_, e) => OnFileSystemEvent(library.Id, e.FullPath);
            watcher.Renamed += (_, e) => OnFileSystemEvent(library.Id, e.FullPath);
            watcher.Error += (_, e) => logger.LogWarning(e.GetException(), "FileSystemWatcher error for library {LibraryId}", library.Id);
            watcher.EnableRaisingEvents = true;

            _watchedLibraries[library.Id] = new WatchedLibrary(library.Id, library.RootPath, watcher);
            logger.LogInformation("Started realtime monitor for library {LibraryId} at {RootPath}", library.Id, library.RootPath);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to start realtime monitor for library {LibraryId}", library.Id);
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
            watched.Watcher.Dispose();
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

    private sealed record WatchedLibrary(Guid LibraryId, string RootPath, FileSystemWatcher Watcher);

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
