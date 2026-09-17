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

    /// <summary>
    /// True when ffmpeg no longer writes to staging <c>index.m4s</c>. The segment muxer
    /// opens <c>index+1</c> only after it closed <c>index</c>. Complete fMP4 boxes are NOT a
    /// closed file: <c>frag_keyframe</c> flushes a moof+mdat at every collapsed interior
    /// keyframe, so a mid-segment snapshot walks as valid but misses the rest of the GOP.
    /// Promoting (copying) it froze a truncated segment into the immutable shared cache
    /// and left a video hole up to the next playlist boundary.
    /// </summary>
    public static bool IsStagingSegmentClosed(string stagingDirectory, int index, bool ffmpegExited)
    {
        if (ffmpegExited)
            return true;

        var nextPath = Path.Combine(stagingDirectory, $"{(index + 1).ToString(CultureInfo.InvariantCulture)}.m4s");
        return File.Exists(nextPath);
    }

    /// <summary>
    /// Best-effort removal of a staging copy whose segment is already in the shared cache.
    /// </summary>
    public static bool TryDeleteStagingSegment(string stagingDirectory, int index)
    {
        var stagingPath = Path.Combine(stagingDirectory, $"{index.ToString(CultureInfo.InvariantCulture)}.m4s");
        try
        {
            if (!File.Exists(stagingPath))
                return false;

            File.Delete(stagingPath);
            return true;
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
