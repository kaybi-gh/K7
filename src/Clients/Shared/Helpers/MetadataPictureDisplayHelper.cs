using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Helpers;

namespace K7.Clients.Shared.Helpers;

public static class MetadataPictureDisplayHelper
{
    public static MetadataPictureSize? GetBestDisplaySize(
        MetadataPictureDto picture,
        params MetadataPictureSize[] preferredSizes)
    {
        if (picture.AvailableSizes.Count > 0)
        {
            foreach (var size in preferredSizes)
            {
                if (picture.AvailableSizes.Contains(size))
                    return size;
            }

            return picture.AvailableSizes[0];
        }

        return null;
    }

    public static bool IsHdStill(MetadataPictureDto? picture)
    {
        if (picture is null || picture.Type != MetadataPictureType.Still)
            return false;

        if (picture.OriginalWidth is > 0
            && picture.OriginalHeight is > 0
            && MetadataPictureThresholds.MeetsHdStillThreshold(picture.OriginalWidth.Value, picture.OriginalHeight.Value))
            return true;

        if (picture.AvailableSizes.Contains(MetadataPictureSize.Medium))
            return true;

        if (picture.OriginalWidth is > 0 && picture.OriginalHeight is > 0)
            return false;

        return true;
    }
}
