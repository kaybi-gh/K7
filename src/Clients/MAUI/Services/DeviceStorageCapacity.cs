namespace K7.Clients.MAUI.Services;

internal static class DeviceStorageCapacity
{
    public static (long? totalBytes, long? availableBytes) GetForAppData()
    {
        try
        {
            return GetForPath(FileSystem.AppDataDirectory);
        }
        catch
        {
            return (null, null);
        }
    }

    private static (long? totalBytes, long? availableBytes) GetForPath(string path)
    {
#if ANDROID
        using var stat = new Android.OS.StatFs(path);
        return (stat.BlockCountLong * stat.BlockSizeLong, stat.AvailableBlocksLong * stat.BlockSizeLong);
#elif IOS || MACCATALYST
        using var url = Foundation.NSUrl.FromFilename(path);
        Foundation.NSError? error = null;
        var keys = new Foundation.NSString[]
        {
            Foundation.NSUrl.VolumeTotalCapacityKey,
            Foundation.NSUrl.VolumeAvailableCapacityForImportantUsageKey,
        };
        var values = url.GetResourceValues(keys, out error);
        if (values is null)
            return (null, null);

        var total = values[Foundation.NSUrl.VolumeTotalCapacityKey] as Foundation.NSNumber;
        var available = values[Foundation.NSUrl.VolumeAvailableCapacityForImportantUsageKey] as Foundation.NSNumber;
        if (total is null || available is null)
            return (null, null);

        return (total.LongValue, available.LongValue);
#else
        var root = Path.GetPathRoot(path);
        if (string.IsNullOrEmpty(root))
            return (null, null);

        var drive = new DriveInfo(root);
        return drive.IsReady ? (drive.TotalSize, drive.AvailableFreeSpace) : (null, null);
#endif
    }
}
