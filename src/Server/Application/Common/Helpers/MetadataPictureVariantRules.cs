using K7.Server.Domain.Enums;

namespace K7.Server.Application.Common.Helpers;

public static class MetadataPictureVariantRules
{
    private static readonly Dictionary<(MetadataPictureType, MetadataPictureSize), int> TargetWidths = new()
    {
        [(MetadataPictureType.Poster, MetadataPictureSize.Small)] = 200,
        [(MetadataPictureType.Poster, MetadataPictureSize.Medium)] = 342,
        [(MetadataPictureType.Cover, MetadataPictureSize.Small)] = 200,
        [(MetadataPictureType.Cover, MetadataPictureSize.Medium)] = 342,
        [(MetadataPictureType.Backdrop, MetadataPictureSize.Small)] = 780,
        [(MetadataPictureType.Backdrop, MetadataPictureSize.Medium)] = 1280,
        [(MetadataPictureType.Portrait, MetadataPictureSize.Small)] = 185,
        [(MetadataPictureType.Portrait, MetadataPictureSize.Medium)] = 342,
        [(MetadataPictureType.Logo, MetadataPictureSize.Small)] = 200,
        [(MetadataPictureType.Logo, MetadataPictureSize.Medium)] = 400,
        [(MetadataPictureType.Still, MetadataPictureSize.Small)] = 640,
        [(MetadataPictureType.Still, MetadataPictureSize.Medium)] = 1280,
    };

    public static bool TryGetTargetWidth(
        MetadataPictureType pictureType,
        MetadataPictureSize size,
        out int targetWidth) =>
        TargetWidths.TryGetValue((pictureType, size), out targetWidth);

    public static bool ShouldGenerateVariant(MetadataPictureType pictureType, MetadataPictureSize size, int originalWidth) =>
        TryGetTargetWidth(pictureType, size, out var targetWidth) && originalWidth > targetWidth;

    public static bool IsPermanentVariantFallback(
        MetadataPictureType pictureType,
        MetadataPictureSize size,
        int? originalWidth)
    {
        if (originalWidth is not > 0 || !TryGetTargetWidth(pictureType, size, out var targetWidth))
            return false;

        return originalWidth <= targetWidth;
    }
}
