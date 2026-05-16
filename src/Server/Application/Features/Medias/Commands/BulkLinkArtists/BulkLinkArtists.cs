using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Medias;
using K7.Shared.Dtos.Requests;

namespace K7.Server.Application.Features.Medias.Commands.BulkLinkArtists;

[Authorize(Roles = Roles.Administrator)]
public record BulkLinkArtistsCommand : IRequest<int>
{
    public required IReadOnlyList<BulkLinkArtistsRequest.ArtistLinkItem> Items { get; init; }
}

public class BulkLinkArtistsCommandHandler(IApplicationDbContext context)
    : IRequestHandler<BulkLinkArtistsCommand, int>
{
    public async Task<int> Handle(BulkLinkArtistsCommand request, CancellationToken cancellationToken)
    {
        var artistNames = request.Items
            .Select(i => i.ArtistName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Find or create MusicArtist media entities
        var existingArtists = await context.Medias.OfType<MusicArtist>()
            .Where(a => artistNames.Contains(a.Title!))
            .ToListAsync(cancellationToken);

        var artistCache = new Dictionary<string, MusicArtist>(StringComparer.OrdinalIgnoreCase);
        foreach (var artist in existingArtists)
        {
            if (artist.Title is not null)
                artistCache.TryAdd(artist.Title, artist);
        }

        var newArtists = new List<MusicArtist>();
        foreach (var name in artistNames)
        {
            if (!artistCache.ContainsKey(name))
            {
                var artist = new MusicArtist { Title = name };
                context.Medias.Add(artist);
                newArtists.Add(artist);
                artistCache[name] = artist;
            }
        }

        if (newArtists.Count > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        // Load target medias
        var mediaIds = request.Items.Select(i => i.MediaId).Distinct().ToList();
        var medias = await context.Medias
            .Where(m => mediaIds.Contains(m.Id))
            .ToListAsync(cancellationToken);

        var mediaMap = medias.ToDictionary(m => m.Id);

        // Link artists to albums/tracks via ArtistId
        var linked = 0;
        foreach (var item in request.Items)
        {
            if (!artistCache.TryGetValue(item.ArtistName, out var artist)) continue;
            if (!mediaMap.TryGetValue(item.MediaId, out var media)) continue;

            switch (media)
            {
                case MusicAlbum album when album.ArtistId != artist.Id:
                    album.ArtistId = artist.Id;
                    linked++;
                    break;
                case MusicTrack track when track.ArtistId != artist.Id:
                    track.ArtistId = artist.Id;
                    linked++;
                    break;
            }
        }

        if (linked > 0)
        {
            await context.SaveChangesAsync(cancellationToken);
        }

        return linked;
    }
}
