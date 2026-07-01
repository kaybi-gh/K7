namespace K7.Clients.Shared.UI.Helpers;

public static class OfflineStoragePresets
{
    private const long Mb = 1024L * 1024;
    private const long Gb = 1024L * 1024 * 1024;

    public static readonly long[] DownloadLimits =
    [
        500 * Mb,
        1 * Gb,
        2 * Gb,
        3 * Gb,
        5 * Gb,
        10 * Gb,
        20 * Gb,
        50 * Gb,
        100 * Gb,
        200 * Gb,
        500 * Gb
    ];

    public static readonly long[] CacheLimits =
    [
        100 * Mb,
        250 * Mb,
        500 * Mb,
        750 * Mb,
        1 * Gb,
        2 * Gb,
        3 * Gb,
        5 * Gb,
        10 * Gb,
        20 * Gb
    ];

    public static readonly int[] LookaheadWifi = [0, 1, 2, 3, 5, 10, 15, 20, 30, 50, 75, 100];

    public static readonly int[] LookaheadMobile = [0, 1, 2, 3, 5, 10, 15, 20, 30, 50];
}
