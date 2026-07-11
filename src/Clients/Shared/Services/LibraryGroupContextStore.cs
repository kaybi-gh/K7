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

public sealed class LibraryGroupContextStore : ILibraryGroupContextStore, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IMediaBrowseHubCoordinator _hubCoordinator;

    private readonly Dictionary<Guid, LibraryGroupContextSnapshot> _contextCache = [];
    private readonly Dictionary<TagsCacheKey, MediaTagsDto> _tagsCache = [];
    private readonly Dictionary<Guid, IDisposable> _subscriptions = [];
    private readonly object _sync = new();

    public event Action<Guid>? Changed;

    public LibraryGroupContextStore(IServiceScopeFactory scopeFactory, IMediaBrowseHubCoordinator hubCoordinator)
    {
        _scopeFactory = scopeFactory;
        _hubCoordinator = hubCoordinator;
    }

    public async Task<LibraryGroupContextSnapshot?> EnsureContextAsync(
        Guid groupId,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            if (_contextCache.TryGetValue(groupId, out var cached))
                return cached;
        }

        using var scope = _scopeFactory.CreateScope();
        var libraryService = scope.ServiceProvider.GetRequiredService<ILibraryService>();

        var groups = await libraryService.GetLibraryGroupsAsync(cancellationToken);
        var group = groups.FirstOrDefault(g => g.Id == groupId);
        if (group is null)
            return null;

        var snapshot = new LibraryGroupContextSnapshot { Group = group };

        lock (_sync)
        {
            _contextCache[groupId] = snapshot;
            EnsureSubscription(groupId, group);
        }

        return snapshot;
    }

    public async Task<MediaTagsDto?> EnsureTagsAsync(
        Guid groupId,
        MediaType? mediaType,
        CancellationToken cancellationToken = default)
    {
        var context = await EnsureContextAsync(groupId, cancellationToken);
        if (context is null)
            return null;

        var cacheKey = new TagsCacheKey(groupId, mediaType ?? default);
        lock (_sync)
        {
            if (_tagsCache.TryGetValue(cacheKey, out var cachedTags))
                return cachedTags;
        }

        using var scope = _scopeFactory.CreateScope();
        var mediaService = scope.ServiceProvider.GetRequiredService<IMediaService>();

        try
        {
            var tags = await mediaService.GetMediaTagsAsync(new GetMediaTagsQuery
            {
                LibraryIds = context.LibraryIds.ToArray(),
                LibraryGroupIds = [groupId],
                MediaTypes = mediaType is { } selected && selected != default ? [selected] : null,
                Kinds =
                [
                    MetadataTagKind.Genre,
                    MetadataTagKind.ContentRating,
                    MetadataTagKind.Studio,
                    MetadataTagKind.Network
                ],
                OrderBy = [MediaTagOrderingOption.MediaCountDesc],
                PageNumber = 1,
                PageSize = 100
            }, cancellationToken);

            if (tags is not null)
            {
                lock (_sync)
                {
                    _tagsCache[cacheKey] = tags;
                }
            }

            return tags;
        }
        catch
        {
            return null;
        }
    }

    public void Invalidate(Guid groupId)
    {
        lock (_sync)
        {
            _contextCache.Remove(groupId);
            var keysToRemove = _tagsCache.Keys.Where(k => k.GroupId == groupId).ToList();
            foreach (var key in keysToRemove)
                _tagsCache.Remove(key);
        }

        Changed?.Invoke(groupId);
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions.Values)
            subscription.Dispose();

        _subscriptions.Clear();
    }

    private void EnsureSubscription(Guid groupId, LibraryGroupDto group)
    {
        if (_subscriptions.ContainsKey(groupId))
            return;

        var libraryGroupIds = new[] { groupId };
        var libraryIds = group.LibraryIds.ToArray();
        _subscriptions[groupId] = _hubCoordinator.Subscribe(libraryIds, libraryGroupIds, () => Invalidate(groupId));
    }

    private readonly record struct TagsCacheKey(Guid GroupId, MediaType MediaType);
}
