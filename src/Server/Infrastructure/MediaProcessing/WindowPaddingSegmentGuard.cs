namespace K7.Server.Infrastructure.MediaProcessing;

/// <summary>
/// ffmpeg windows pad one playlist index before/after the deliver range so midpoint
/// seek + -segment_times cut on the requested keyframes. Those pad files use real
/// playlist numbers. Restore a previously-ready pad. Keep a new after-pad (next
/// playlist index). Delete only a new before-pad (IDR snap garbage).
/// </summary>
internal sealed class WindowPaddingSegmentGuard
{
    private const int ReadySegmentMinBytes = 32;

    private readonly string _outputDirectory;
    private readonly int? _beforeIndex;
    private readonly int? _afterIndex;
    private readonly byte[]? _beforeBackup;
    private readonly byte[]? _afterBackup;

    private WindowPaddingSegmentGuard(
        string outputDirectory,
        int? beforeIndex,
        int? afterIndex,
        byte[]? beforeBackup,
        byte[]? afterBackup)
    {
        _outputDirectory = outputDirectory;
        _beforeIndex = beforeIndex;
        _afterIndex = afterIndex;
        _beforeBackup = beforeBackup;
        _afterBackup = afterBackup;
    }

    public static WindowPaddingSegmentGuard Capture(
        string outputDirectory,
        int ffmpegStartIndex,
        int deliverStartIndex,
        int deliverEndIndexExclusive,
        int ffmpegEndIndexExclusive)
    {
        int? before = ffmpegStartIndex < deliverStartIndex ? ffmpegStartIndex : null;
        int? after = ffmpegEndIndexExclusive > deliverEndIndexExclusive
            ? deliverEndIndexExclusive
            : null;

        return new WindowPaddingSegmentGuard(
            outputDirectory,
            before,
            after,
            ReadIfReady(outputDirectory, before),
            ReadIfReady(outputDirectory, after));
    }

    public void RestoreOrDiscard()
    {
        Apply(_beforeIndex, _beforeBackup, deleteIfNew: true);
        Apply(_afterIndex, _afterBackup, deleteIfNew: false);
    }

    private void Apply(int? index, byte[]? backup, bool deleteIfNew)
    {
        if (index is not int segmentIndex)
            return;

        var path = SegmentPath(_outputDirectory, segmentIndex);
        if (backup is not null)
        {
            try
            {
                File.WriteAllBytes(path, backup);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }

            return;
        }

        if (deleteIfNew)
            TryDelete(path);
    }

    private static byte[]? ReadIfReady(string outputDirectory, int? index)
    {
        if (index is not int segmentIndex)
            return null;

        var path = SegmentPath(outputDirectory, segmentIndex);
        try
        {
            if (!File.Exists(path))
                return null;

            var info = new FileInfo(path);
            if (info.Length < ReadySegmentMinBytes)
                return null;

            return File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static string SegmentPath(string outputDirectory, int segmentIndex) =>
        Path.Combine(outputDirectory, $"{segmentIndex}.m4s");
}
