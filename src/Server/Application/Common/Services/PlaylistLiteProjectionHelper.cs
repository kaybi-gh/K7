using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Common.Services;

public static class PlaylistLiteProjectionHelper
{
    private const int MaxPreviewCount = 4;
    private const int PreviewCandidateWindow = 32;

    private sealed record PlaylistItemRow(Guid PlaylistId, Guid MediaId, int Order);
    private sealed record MediaInfoRow(Guid Id, MediaType Type, Guid? AlbumId);
    private sealed record PictureRow(
        Guid Id,
        Guid MediaId,
        MetadataPictureType Type,
        bool IsLocal,
        string? DominantColor,
        int? OriginalWidth,
        int? OriginalHeight);

    public static async Task<IReadOnlyDictionary<Guid, int>> GetItemCountsByPlaylistIdAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> playlistIds,
        CancellationToken cancellationToken = default)
    {
        if (playlistIds.Count == 0)
            return new Dictionary<Guid, int>();

        var idSet = playlistIds as HashSet<Guid> ?? playlistIds.ToHashSet();
        return await context.PlaylistItems
            .AsNoTracking()
            .Where(i => idSet.Contains(i.PlaylistId))
            .GroupBy(i => i.PlaylistId)
            .Select(g => new { PlaylistId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PlaylistId, x => x.Count, cancellationToken);
    }

    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureDto>>> GetPreviewPicturesByPlaylistIdAsync(
        IApplicationDbContext context,
        IReadOnlyCollection<Guid> playlistIds,
        CancellationToken cancellationToken = default)
    {
        if (playlistIds.Count == 0)
            return new Dictionary<Guid, IReadOnlyList<MetadataPictureDto>>();

        var idSet = playlistIds as HashSet<Guid> ?? playlistIds.ToHashSet();

        var itemRows = await context.PlaylistItems
            .AsNoTracking()
            .Where(i => idSet.Contains(i.PlaylistId))
            .Select(i => new PlaylistItemRow(i.PlaylistId, i.MediaId, i.Order))
            .ToListAsync(cancellationToken);

        var candidateMediaIdsByPlaylist = itemRows
            .GroupBy(i => i.PlaylistId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(i => i.Order).Take(PreviewCandidateWindow).Select(i => i.MediaId).ToList());

        var candidateMediaIds = candidateMediaIdsByPlaylist.Values.SelectMany(ids => ids).ToHashSet();
        if (candidateMediaIds.Count == 0)
        {
            return idSet.ToDictionary(
                id => id,
                _ => (IReadOnlyList<MetadataPictureDto>)Array.Empty<MetadataPictureDto>());
        }

        var mediaTypes = await context.Medias
            .AsNoTracking()
            .Where(m => candidateMediaIds.Contains(m.Id))
            .Select(m => new { m.Id, m.Type })
            .ToListAsync(cancellationToken);

        var trackAlbumIds = await context.Medias
            .OfType<MusicTrack>()
            .AsNoTracking()
            .Where(t => candidateMediaIds.Contains(t.Id))
            .Select(t => new { t.Id, t.AlbumId })
            .ToDictionaryAsync(t => t.Id, t => t.AlbumId, cancellationToken);

        var mediaInfoById = mediaTypes.ToDictionary(
            m => m.Id,
            m => new MediaInfoRow(
                m.Id,
                m.Type,
                trackAlbumIds.TryGetValue(m.Id, out var albumId) ? albumId : null));

        var pictureMediaIds = candidateMediaIds
            .Concat(trackAlbumIds.Values)
            .ToHashSet();

        var pictures = await context.MetadataPictures
            .AsNoTracking()
            .Where(p => p.MediaId.HasValue && pictureMediaIds.Contains(p.MediaId.Value))
            .Select(p => new PictureRow(
                p.Id,
                p.MediaId!.Value,
                p.Type,
                p.LocalPath != null,
                p.DominantColor,
                p.OriginalWidth,
                p.OriginalHeight))
            .ToListAsync(cancellationToken);

        var picturesByMediaId = pictures
            .GroupBy(p => p.MediaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var selectedPictureIds = new List<Guid>();
        var selectedByPlaylist = new Dictionary<Guid, List<PictureRow>>();

        foreach (var playlistId in idSet)
        {
            var selected = new List<PictureRow>();
            if (!candidateMediaIdsByPlaylist.TryGetValue(playlistId, out var mediaIds))
            {
                selectedByPlaylist[playlistId] = selected;
                continue;
            }

            foreach (var mediaId in mediaIds)
            {
                if (selected.Count >= MaxPreviewCount)
                    break;

                if (!mediaInfoById.TryGetValue(mediaId, out var mediaInfo))
                    continue;

                var picture = SelectPrimaryPicture(mediaInfo, picturesByMediaId);
                if (picture is null)
                    continue;

                selected.Add(picture);
                selectedPictureIds.Add(picture.Id);
            }

            selectedByPlaylist[playlistId] = selected;
        }

        var pictureSizes = await MetadataPictureSizesHelper.GetAvailableSizesByPictureIdsAsync(
            context,
            selectedPictureIds,
            cancellationToken);

        return selectedByPlaylist.ToDictionary(
            kvp => kvp.Key,
            IReadOnlyList<MetadataPictureDto> (kvp) => kvp.Value.Select(p => MapPicture(p, pictureSizes)).ToList());
    }

    private static PictureRow? SelectPrimaryPicture(
        MediaInfoRow media,
        IReadOnlyDictionary<Guid, List<PictureRow>> picturesByMediaId)
    {
        picturesByMediaId.TryGetValue(media.Id, out var mediaPictures);
        mediaPictures ??= [];

        if (media.Type == MediaType.MusicTrack)
        {
            picturesByMediaId.TryGetValue(media.AlbumId ?? Guid.Empty, out var albumPictures);
            albumPictures ??= [];

            return FirstOfType(mediaPictures, MetadataPictureType.Cover)
                ?? FirstOfType(albumPictures, MetadataPictureType.Cover)
                ?? mediaPictures.FirstOrDefault()
                ?? albumPictures.FirstOrDefault();
        }

        var preferredType = media.Type switch
        {
            MediaType.MusicAlbum or MediaType.MusicArtist => MetadataPictureType.Cover,
            MediaType.SerieEpisode => MetadataPictureType.Still,
            _ => MetadataPictureType.Poster
        };

        return FirstOfType(mediaPictures, preferredType)
            ?? FirstOfType(mediaPictures, MetadataPictureType.Poster)
            ?? FirstOfType(mediaPictures, MetadataPictureType.Cover)
            ?? FirstOfType(mediaPictures, MetadataPictureType.Still)
            ?? mediaPictures.FirstOrDefault();
    }

    private static PictureRow? FirstOfType(IReadOnlyList<PictureRow> pictures, MetadataPictureType type) =>
        pictures.FirstOrDefault(p => p.Type == type);

    private static MetadataPictureDto MapPicture(
        PictureRow picture,
        IReadOnlyDictionary<Guid, IReadOnlyList<MetadataPictureSize>> sizes) => new()
        {
            Id = picture.Id,
            Type = picture.Type,
            Uri = picture.IsLocal
                ? new Uri($"/api/metadata-pictures/{picture.Id}", UriKind.Relative)
                : null,
            DominantColor = picture.DominantColor,
            OriginalWidth = picture.OriginalWidth,
            OriginalHeight = picture.OriginalHeight,
            AvailableSizes = sizes.GetValueOrDefault(picture.Id) ?? []
        };
}
