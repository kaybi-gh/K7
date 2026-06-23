using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.Extensions;
using K7.Shared.Interfaces;

namespace K7.Clients.Shared.Helpers;

public static class MediaBrowseTagSearch
{
    public static async Task<IReadOnlyList<string>> SearchAsync(
        IMediaService mediaService,
        string field,
        string searchText,
        Guid[]? libraryIds,
        Guid[]? libraryGroupIds,
        MediaType mediaType,
        CancellationToken cancellationToken)
    {
        if (field is nameof(SmartPlaylistField.ActorName) or nameof(SmartPlaylistField.ArtistName))
        {
            var results = await mediaService.GetMediaBrowseFilterSuggestionsAsync(
                new GetMediaBrowseFilterSuggestionsQuery
                {
                    LibraryIds = libraryIds,
                    LibraryGroupIds = libraryGroupIds,
                    MediaTypes = mediaType != default ? [mediaType] : null,
                    Field = field,
                    SearchText = searchText,
                    Limit = 20
                },
                cancellationToken);

            return results ?? [];
        }

        if (!TryMapFieldToKind(field, out var kind))
            return [];

        var tags = await mediaService.GetMediaTagsAsync(
            new GetMediaTagsQuery
            {
                LibraryIds = libraryIds,
                LibraryGroupIds = libraryGroupIds,
                MediaTypes = mediaType != default ? [mediaType] : null,
                Kinds = [kind],
                SearchText = searchText,
                Limit = 20
            },
            cancellationToken);

        return tags.GetValues(kind);
    }

    public static bool TryMapFieldToKind(string field, out MetadataTagKind kind) => field switch
    {
        "Studio" => Set(MetadataTagKind.Studio, out kind),
        "Network" => Set(MetadataTagKind.Network, out kind),
        nameof(RestrictionField.ContentRating) => Set(MetadataTagKind.ContentRating, out kind),
        nameof(SmartPlaylistField.Genre) or nameof(RestrictionField.Genre) => Set(MetadataTagKind.Genre, out kind),
        _ => Set(default, out kind) && false
    };

    private static bool Set(MetadataTagKind value, out MetadataTagKind kind)
    {
        kind = value;
        return true;
    }
}
