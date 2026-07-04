namespace K7.Server.Application.Helpers;

public static class FileTimestampHelper
{
    private static readonly TimeSpan ModificationTolerance = TimeSpan.FromSeconds(1);

    public static bool HasSameContent(DateTimeOffset storedLastWriteTimeUtc, long storedSize, DateTimeOffset diskLastWriteTimeUtc, long diskSize)
    {
        if (storedSize != diskSize)
            return false;

        if (storedLastWriteTimeUtc == default)
            return true;

        return Math.Abs((storedLastWriteTimeUtc - diskLastWriteTimeUtc).TotalSeconds) <= ModificationTolerance.TotalSeconds;
    }

    public static bool NeedsLastWriteTimeBackfill(DateTimeOffset storedLastWriteTimeUtc) => storedLastWriteTimeUtc == default;
}
