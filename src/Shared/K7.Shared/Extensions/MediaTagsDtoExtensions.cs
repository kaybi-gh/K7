using K7.Server.Domain.Enums;
using K7.Shared.Dtos;

namespace K7.Shared.Extensions;

public static class MediaTagsDtoExtensions
{
    public static MediaTagKindValuesDto? GetKind(this MediaTagsDto? tags, MetadataTagKind kind) =>
        tags?.Kinds.FirstOrDefault(k => k.Kind == kind);

    public static IReadOnlyList<string> GetValues(this MediaTagsDto? tags, MetadataTagKind kind) =>
        GetKind(tags, kind)?.Values.Select(v => v.DisplayName).ToList() ?? [];

    public static IReadOnlyList<MediaTagValueDto> GetTagValues(this MediaTagsDto? tags, MetadataTagKind kind) =>
        GetKind(tags, kind)?.Values ?? [];
}
