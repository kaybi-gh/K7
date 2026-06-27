using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;

namespace K7.Clients.Shared.Helpers;

public static class LibraryGroupBrowseNavigationHelper
{
    public static Guid? ResolveGroupId(
        IReadOnlyList<LibraryGroupDto> groups,
        Guid? libraryId,
        LibraryMediaType mediaType)
    {
        if (libraryId.HasValue)
        {
            var match = groups.FirstOrDefault(g =>
                g.MediaType == mediaType && g.LibraryIds.Contains(libraryId.Value));
            if (match is not null)
                return match.Id;
        }

        return groups.FirstOrDefault(g => g.MediaType == mediaType)?.Id;
    }

    public static string BuildBrowseUrl(
        Guid groupId,
        string? genre = null,
        string? studio = null,
        string? network = null,
        MediaType? mediaType = null)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(genre))
            query.Add($"genre={Uri.EscapeDataString(genre)}");

        if (!string.IsNullOrWhiteSpace(studio))
            query.Add($"studio={Uri.EscapeDataString(studio)}");

        if (!string.IsNullOrWhiteSpace(network))
            query.Add($"network={Uri.EscapeDataString(network)}");

        if (mediaType is not null && mediaType != default)
            query.Add($"mediaType={mediaType.Value}");

        return query.Count == 0
            ? $"/library-groups/{groupId}"
            : $"/library-groups/{groupId}?{string.Join("&", query)}";
    }
}
