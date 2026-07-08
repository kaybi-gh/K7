namespace K7.Shared.Helpers;

public static class MetadataPictureThresholds
{
    public const int MinHdStillWidth = 1280;
    public const int MinHdStillHeight = 720;

    public static bool MeetsHdStillThreshold(int width, int height) =>
        width >= MinHdStillWidth || height >= MinHdStillHeight;
}
