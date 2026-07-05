using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas;
using K7.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Medias.Services;

public interface IMediaMetadataTagSyncService
{
    Task ApplyTagsAsync(BaseMedia media, IReadOnlyList<MetadataTagDesired> desired, CancellationToken cancellationToken = default);

    Task ApplyTagsAsync(Guid mediaId, IReadOnlyList<MetadataTagDesired> desired, CancellationToken cancellationToken = default);
}

public sealed class MediaMetadataTagSyncService(IApplicationDbContext context) : IMediaMetadataTagSyncService
{
    private readonly Dictionary<(MetadataTagKind Kind, string NormalizedKey), MetadataTag> _tagCache = new();

    public async Task ApplyTagsAsync(Guid mediaId, IReadOnlyList<MetadataTagDesired> desired, CancellationToken cancellationToken = default)
    {
        var media = await context.Medias
            .Include(m => m.MetadataTags)
            .ThenInclude(mt => mt.MetadataTag)
            .FirstOrDefaultAsync(m => m.Id == mediaId, cancellationToken);

        if (media is null)
            return;

        await ApplyTagsAsync(media, desired, cancellationToken);
    }

    public async Task ApplyTagsAsync(BaseMedia media, IReadOnlyList<MetadataTagDesired> desired, CancellationToken cancellationToken = default)
    {
        if (desired.Count == 0)
            return;

        var uniqueDesired = desired
            .GroupBy(t => (t.Kind, t.NormalizedKey))
            .Select(g => g.First())
            .ToList();

        var kinds = uniqueDesired.Select(t => t.Kind).Distinct().ToList();
        var desiredKeys = uniqueDesired
            .Select(t => (t.Kind, t.NormalizedKey))
            .ToHashSet();

        await PreloadTagsAsync(uniqueDesired, cancellationToken);

        var toRemove = media.MetadataTags
            .Where(mt => kinds.Contains(mt.MetadataTag.Kind)
                && !desiredKeys.Contains((mt.MetadataTag.Kind, mt.MetadataTag.NormalizedKey)))
            .ToList();

        foreach (var link in toRemove)
            context.MediaMetadataTags.Remove(link);

        foreach (var tag in uniqueDesired)
        {
            var metadataTag = await GetOrCreateTagAsync(tag.Kind, tag.NormalizedKey, tag.DisplayName, cancellationToken);
            if (media.MetadataTags.Any(mt => mt.MetadataTagId == metadataTag.Id))
                continue;

            context.MediaMetadataTags.Add(new MediaMetadataTag
            {
                MediaId = media.Id,
                Media = media,
                MetadataTagId = metadataTag.Id,
                MetadataTag = metadataTag
            });
        }
    }

    private async Task PreloadTagsAsync(
        IReadOnlyList<MetadataTagDesired> desired,
        CancellationToken cancellationToken)
    {
        RegisterLocalTags();

        var missingKeys = desired
            .Select(t => (t.Kind, t.NormalizedKey))
            .Where(key => !_tagCache.ContainsKey(key))
            .Distinct()
            .ToList();

        if (missingKeys.Count == 0)
            return;

        var kinds = missingKeys.Select(k => k.Kind).Distinct().ToList();
        var normalizedKeys = missingKeys.Select(k => k.NormalizedKey).Distinct().ToList();

        var existing = await context.MetadataTags
            .Where(t => kinds.Contains(t.Kind) && normalizedKeys.Contains(t.NormalizedKey))
            .ToListAsync(cancellationToken);

        foreach (var tag in existing)
            RegisterTag(tag);
    }

    private async Task<MetadataTag> GetOrCreateTagAsync(
        MetadataTagKind kind,
        string normalizedKey,
        string displayName,
        CancellationToken cancellationToken)
    {
        var key = (kind, normalizedKey);

        if (_tagCache.TryGetValue(key, out var cached))
            return cached;

        RegisterLocalTags();

        if (_tagCache.TryGetValue(key, out cached))
            return cached;

        var existing = await context.MetadataTags
            .FirstOrDefaultAsync(t => t.Kind == kind && t.NormalizedKey == normalizedKey, cancellationToken);

        if (existing is not null)
        {
            RegisterTag(existing);
            return existing;
        }

        var tag = new MetadataTag
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            NormalizedKey = normalizedKey,
            DisplayName = displayName
        };
        context.MetadataTags.Add(tag);
        RegisterTag(tag);
        return tag;
    }

    private void RegisterLocalTags()
    {
        foreach (var tag in context.MetadataTags.Local)
            RegisterTag(tag);
    }

    private void RegisterTag(MetadataTag tag) =>
        _tagCache[(tag.Kind, tag.NormalizedKey)] = tag;
}
