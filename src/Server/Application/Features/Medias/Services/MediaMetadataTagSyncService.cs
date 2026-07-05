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

        var kinds = desired.Select(t => t.Kind).ToHashSet();
        var desiredKeys = desired
            .Select(t => (t.Kind, t.NormalizedKey))
            .ToHashSet();

        var toRemove = media.MetadataTags
            .Where(mt => kinds.Contains(mt.MetadataTag.Kind)
                && !desiredKeys.Contains((mt.MetadataTag.Kind, mt.MetadataTag.NormalizedKey)))
            .ToList();

        foreach (var link in toRemove)
            context.MediaMetadataTags.Remove(link);

        foreach (var tag in desired)
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

    private async Task<MetadataTag> GetOrCreateTagAsync(
        MetadataTagKind kind,
        string normalizedKey,
        string displayName,
        CancellationToken cancellationToken)
    {
        var tracked = context.MetadataTags.Local
            .FirstOrDefault(t => t.Kind == kind && t.NormalizedKey == normalizedKey);

        if (tracked is not null)
            return tracked;

        var existing = await context.MetadataTags
            .FirstOrDefaultAsync(t => t.Kind == kind && t.NormalizedKey == normalizedKey, cancellationToken);

        if (existing is not null)
            return existing;

        var tag = new MetadataTag
        {
            Id = Guid.NewGuid(),
            Kind = kind,
            NormalizedKey = normalizedKey,
            DisplayName = displayName
        };
        context.MetadataTags.Add(tag);
        return tag;
    }
}
