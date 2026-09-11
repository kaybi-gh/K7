using System.Globalization;

namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// Promotes remux head staging files into the shared output directory without overwriting
/// ready segments (immutable shared cache).
/// </summary>
internal static class RemuxSegmentPromoter
{
    public static bool TryPromoteMediaSegment(string stagingDirectory, string sharedDirectory, int index)
    {
        var stagingPath = Path.Combine(stagingDirectory, $"{index.ToString(CultureInfo.InvariantCulture)}.m4s");
        var sharedPath = Path.Combine(sharedDirectory, $"{index.ToString(CultureInfo.InvariantCulture)}.m4s");
        return TryPromoteFile(stagingPath, sharedPath);
    }

    public static bool TryPromoteInit(string stagingDirectory, string sharedDirectory)
    {
        var stagingPath = Path.Combine(stagingDirectory, "init.m4s");
        var sharedPath = Path.Combine(sharedDirectory, "init.m4s");
        return TryPromoteFile(stagingPath, sharedPath);
    }

    private static bool TryPromoteFile(string stagingPath, string sharedPath)
    {
        try
        {
            if (!File.Exists(stagingPath))
                return false;

            var stagingInfo = new FileInfo(stagingPath);
            if (stagingInfo.Length < 32)
                return false;

            if (File.Exists(sharedPath))
            {
                var sharedInfo = new FileInfo(sharedPath);
                if (sharedInfo.Length >= 32)
                    return false;

                try
                {
                    File.Delete(sharedPath);
                }
                catch (IOException)
                {
                    return false;
                }
            }

            Directory.CreateDirectory(Path.GetDirectoryName(sharedPath)!);
            var tempPath = sharedPath + ".promoting";
            try
            {
                File.Copy(stagingPath, tempPath, overwrite: true);
                // Never overwrite a ready shared segment (atomic fail if another head won).
                File.Move(tempPath, sharedPath, overwrite: false);
                return true;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempPath))
                        File.Delete(tempPath);
                }
                catch (IOException)
                {
                }
            }
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
