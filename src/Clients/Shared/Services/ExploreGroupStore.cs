using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;

public sealed class ExploreGroupStore : IExploreGroupStore, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMediaBrowseHubCoordinator _hubCoordinator;

    private readonly Dictionary<Guid, ExploreGroupSnapshot> _cache = [];
    private readonly Dictionary<Guid, IDisposable> _subscriptions = [];
    private readonly object _sync = new();

    public event Action<Guid>? Changed;

    public ExploreGroupStore(IServiceScopeFactory scopeFactory, IMediaBrowseHubCoordinator hubCoordinator)
    {
        _scopeFactory = scopeFactory;
        _hubCoordinator = hubCoordinator;
    }

    public async Task<ExploreGroupSnapshot?> EnsureGroupAsync(Guid groupId, CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_cache.TryGetValue(groupId, out var cached))
                return cached;
        }

        using var scope = _scopeFactory.CreateScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<ILibraryService>();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();

        var groups = await libraryService.GetLibraryGroupsAsync(cancellationToken);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
            return null;

        var genres = group.MediaType is LibraryMediaType.Movie or LibraryMediaType.Serie
            ? await LoadGenresAsync(mediaService, group, cancellationToken)
            : [];

        var snapshot = new ExploreGroupSnapshot
        {
            Group = group,
            Genres = genres
        };

        lock (_sync)
        {
            _cache[groupId] = snapshot;
            if (!_subscriptions.ContainsKey(groupId))
            {
                var libraryGroupIds = new[] { groupId };
                var libraryIds = group.LibraryIds.ToArray();
                _subscriptions[groupId] = _hubCoordinator.Subscribe(libraryIds, libraryGroupIds, () => Invalidate(groupId));
            }
        }

        return snapshot;
    }

    public void Invalidate(Guid groupId)
    {
        lock (_sync)
        {
            _cache.Remove(groupId);
        }

        Changed?.Invoke(groupId);
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions.Values)
            subscription.Dispose();

        _subscriptions.Clear();
    }

    private static async Task<List<MediaTagValueDto>> LoadGenresAsync(
        IMediaService mediaService,
        LibraryGroupDto group,
        CancellationToken cancellationToken)
    {
        var mediaTypes = group.MediaType switch
        {
            LibraryMediaType.Movie => new HashSet<MediaType> { MediaType.Movie },
            LibraryMediaType.Serie => new HashSet<MediaType> { MediaType.Serie },
            _ => null
        };

        if (mediaTypes is null)
            return [];

        try
        {
            var result = await mediaService.GetMediaTagsAsync(new GetMediaTagsQuery
            {
                LibraryGroupIds = [group.Id],
                MediaTypes = [.. mediaTypes],
                Kinds = [MetadataTagKind.Genre],
                OrderBy =
                [
                    MediaTagOrderingOption.UserPlayCountDesc,
                    MediaTagOrderingOption.MediaCountDesc
                ],
                PageNumber = 1,
                PageSize = 3
            }, cancellationToken);

            return result?.GetTagValues(MetadataTagKind.Genre).ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
