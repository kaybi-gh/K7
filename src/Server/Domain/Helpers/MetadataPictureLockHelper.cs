using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Helpers;

public static class MetadataPictureLockHelper
{
    public const string AllPicturesField = "Pictures";

    public static string GetTypeField(MetadataPictureType type) => $"{AllPicturesField}:{type}";

    public static bool IsPictureTypeLocked(IEnumerable<string> lockedFields, MetadataPictureType type)
    {
        var fields = lockedFields as IReadOnlyList<string> ?? lockedFields.ToList();
        return fields.Contains(AllPicturesField) || fields.Contains(GetTypeField(type));
    }
}
