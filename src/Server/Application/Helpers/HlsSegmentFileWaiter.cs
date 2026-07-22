using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Waits for HLS segment files produced by on-demand ffmpeg transcoding.
/// Re-kicks generation when ffmpeg exits without producing the segment, and
/// keeps waiting while a job is actively generating.
/// </summary>
internal static class HlsSegmentFileWaiter
{
    public const string InitSegmentFileName = "init.m4s";

    /// <summary>
    /// ISO BMFF size==0 means "box extends to EOF". While ffmpeg is still appending,
    /// a partial mdat with size 0 walks as "complete" and ExoPlayer then hits
    /// "Top bit not zero" when parsing truncated sample headers. Require the file
    /// length to stay unchanged for this window before accepting such snapshots.
    /// </summary>
    public const int SizeZeroStableMs = 250;

    private static readonly ConcurrentDictionary<string, (long Length, long Timestamp)> SizeZeroStability =
        new(StringComparer.OrdinalIgnoreCase);

    private static ReadOnlySpan<byte> Ftyp => "ftyp"u8;
    private static ReadOnlySpan<byte> Moov => "moov"u8;
    private static ReadOnlySpan<byte> Moof => "moof"u8;
    private static ReadOnlySpan<byte> Mdat => "mdat"u8;
    private static ReadOnlySpan<byte> Traf => "traf"u8;
    private static ReadOnlySpan<byte> Tfhd => "tfhd"u8;
    private static ReadOnlySpan<byte> Tfdt => "tfdt"u8;
    private static ReadOnlySpan<byte> Trun => "trun"u8;

    public static async Task<Exception?> WaitUntilAvailableAsync(
        string segmentPath,
        TranscodeJob job,
        Func<CancellationToken, Task> ensureGenerationAsync,
        CancellationToken cancellationToken,
        int maxTotalSeconds = 180,
        int pollingIntervalMs = 200)
    {
        var absoluteDeadline = DateTime.UtcNow.AddSeconds(maxTotalSeconds);
        var lastKick = DateTime.MinValue;

        // Kick generation with a job-scoped token so a browser abort cannot cancel
        // the ensure/start path before ffmpeg is launched. The poll below still
        // uses the request token so disconnected clients stop waiting.
        await ensureGenerationAsync(CancellationToken.None);

        while (!IsSegmentFileReady(segmentPath) && DateTime.UtcNow < absoluteDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (job.FfmpegTask is { IsFaulted: true } failedTask)
                return failedTask.Exception?.GetBaseException()
                    ?? new InvalidOperationException("FFmpeg exited without generating the requested segment.");

            var ffmpegRunning = job.FfmpegTask is { IsCompleted: false };
            if (!ffmpegRunning && (DateTime.UtcNow - lastKick).TotalSeconds >= 1.5)
            {
                lastKick = DateTime.UtcNow;
                await ensureGenerationAsync(CancellationToken.None);
            }

            await Task.Delay(pollingIntervalMs, cancellationToken);
        }

        return IsSegmentFileReady(segmentPath)
            ? null
            : new TimeoutException("FFmpeg did not generate the requested segment before the timeout.");
    }

    public static async Task WaitUntilReadableAsync(
        string segmentPath,
        CancellationToken cancellationToken,
        int timeoutSeconds = 5)
    {
        var accessDeadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);

        while (DateTime.UtcNow < accessDeadline)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (TryReadReadySegmentBytes(segmentPath, out _))
                return;

            await Task.Delay(100, cancellationToken);
        }
    }

    /// <summary>
    /// Snapshot a ready segment into memory so HTTP responses never stream a file
    /// that ffmpeg may still be writing (truncated fMP4 causes ExoPlayer
    /// "Top bit not zero" ISO BMFF parse failures).
    /// </summary>
    public static bool TryReadReadySegmentBytes(string segmentPath, out byte[] bytes)
    {
        bytes = [];
        if (!File.Exists(segmentPath))
            return false;

        try
        {
            using var stream = new FileStream(
                segmentPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            if (stream.Length <= 0 || stream.Length > int.MaxValue)
                return false;

            var length = (int)stream.Length;
            var buffer = new byte[length];
            var offset = 0;
            while (offset < length)
            {
                var read = stream.Read(buffer, offset, length - offset);
                if (read == 0)
                    return false;
                offset += read;
            }

            // File grew while reading - still being written.
            if (stream.Length != length)
            {
                ClearSizeZeroStability(segmentPath);
                return false;
            }

            if (!TryValidateFmp4Segment(
                    buffer,
                    IsInitSegmentPath(segmentPath),
                    out var hasSizeZeroBox,
                    out _))
            {
                ClearSizeZeroStability(segmentPath);
                return false;
            }

            // size==0 boxes are only safe once the writer has stopped appending.
            // Prefer "next media segment exists" as a stronger completion signal.
            // Keep the stability entry after success so WaitUntilAvailable -> TryRead
            // does not have to re-settle the same length.
            if (hasSizeZeroBox && !IsSizeZeroSnapshotSafe(segmentPath, length))
                return false;

            bytes = buffer;
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static bool IsSegmentFileReady(string segmentPath) =>
        TryReadReadySegmentBytes(segmentPath, out _);

    public static bool HasNonEmptyContent(string segmentPath)
    {
        if (!File.Exists(segmentPath))
            return false;

        try
        {
            return new FileInfo(segmentPath).Length > 0;
        }
        catch (IOException)
        {
            return false;
        }
    }

    /// <summary>
    /// Segment 0 also requires a valid init.m4s; later segments only need their .m4s file.
    /// </summary>
    public static bool IsSegmentReadyOnDisk(string outputDirectory, int segmentIndex)
    {
        var segmentPath = Path.Combine(outputDirectory, $"{segmentIndex}.m4s");
        if (!IsSegmentFileReady(segmentPath))
            return false;

        if (segmentIndex != 0)
            return true;

        return IsSegmentFileReady(GetInitSegmentPath(outputDirectory));
    }

    public static string GetInitSegmentPath(string outputDirectory) =>
        Path.Combine(outputDirectory, InitSegmentFileName);

    public static bool IsValidFmp4Segment(ReadOnlySpan<byte> data, bool isInit) =>
        TryValidateFmp4Segment(data, isInit, out _, out _);

    /// <summary>
    /// Describes top-level boxes for diagnostics (type + size).
    /// </summary>
    public static string DescribeTopLevelBoxes(ReadOnlySpan<byte> data)
    {
        if (!TryWalkCompleteBoxes(
                data,
                out _,
                out _,
                out _,
                out _,
                out _,
                out var description))
        {
            return description.Length > 0 ? description + " (incomplete)" : "(unparseable)";
        }

        return description.Length > 0 ? description : "(no boxes)";
    }

    /// <summary>
    /// Summarizes tfhd/tfdt/trun fields (first sample sizes/CTS, decode time) for server logs.
    /// </summary>
    public static string DescribeMoofTrun(ReadOnlySpan<byte> data)
    {
        if (!TryFindFirstMoof(data, out var moof))
            return "no-moof";

        if (!TryValidateAndDescribeTrun(moof, out var summary, out _))
            return summary;

        return summary;
    }

    public static string FormatLeadingBytesHex(ReadOnlySpan<byte> data, int byteCount = 16)
    {
        var count = Math.Min(byteCount, data.Length);
        if (count <= 0)
            return "(empty)";

        var sb = new StringBuilder(count * 3);
        for (var i = 0; i < count; i++)
        {
            if (i > 0)
                sb.Append(' ');
            sb.Append(data[i].ToString("X2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Why a snapshot is not considered a complete fMP4 (for server logs).
    /// </summary>
    public static string DescribeUnreadiness(string segmentPath)
    {
        if (!File.Exists(segmentPath))
            return "missing";

        try
        {
            using var stream = new FileStream(
                segmentPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);

            var length = stream.Length;
            if (length <= 0)
                return "empty";

            if (length > int.MaxValue)
                return "too-large";

            var buffer = new byte[(int)length];
            var offset = 0;
            while (offset < buffer.Length)
            {
                var read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read == 0)
                    return "short-read";
                offset += read;
            }

            if (stream.Length != length)
                return $"grew-during-read ({length}->{stream.Length})";

            if (!TryValidateFmp4Segment(
                    buffer,
                    IsInitSegmentPath(segmentPath),
                    out var hasSizeZeroBox,
                    out var reason))
            {
                return reason
                    + "; leading="
                    + FormatLeadingBytesHex(buffer)
                    + "; boxes="
                    + DescribeTopLevelBoxes(buffer)
                    + "; trun="
                    + DescribeMoofTrun(buffer);
            }

            if (hasSizeZeroBox)
            {
                return "size-zero-box-awaiting-stable-length"
                    + "; length="
                    + length
                    + "; leading="
                    + FormatLeadingBytesHex(buffer)
                    + "; boxes="
                    + DescribeTopLevelBoxes(buffer);
            }

            return "ready; trun=" + DescribeMoofTrun(buffer);
        }
        catch (IOException ex)
        {
            return "io:" + ex.GetType().Name;
        }
    }

    private static bool TryValidateFmp4Segment(
        ReadOnlySpan<byte> data,
        bool isInit,
        out bool hasSizeZeroBox,
        out string reason)
    {
        hasSizeZeroBox = false;
        reason = "ok";

        if (data.Length < 16)
        {
            reason = "too-short";
            return false;
        }

        if (!TryWalkCompleteBoxes(
                data,
                out var sawFtyp,
                out var sawMoov,
                out var sawMoof,
                out var sawMdat,
                out hasSizeZeroBox,
                out _))
        {
            reason = "incomplete-or-invalid-boxes";
            return false;
        }

        if (isInit)
        {
            if (sawFtyp && sawMoov)
                return true;

            reason = "init-missing-ftyp-or-moov";
            return false;
        }

        if (!sawMoof || !sawMdat)
        {
            reason = "media-missing-moof-or-mdat";
            return false;
        }

        // Media segments must have ExoPlayer-safe trun sample tables.
        if (!TryFindFirstMoof(data, out var moof))
        {
            reason = "moof-unreadable";
            return false;
        }

        if (!TryValidateAndDescribeTrun(moof, out _, out var trunReason))
        {
            reason = trunReason;
            return false;
        }

        return true;
    }

    private static bool TryFindFirstMoof(ReadOnlySpan<byte> data, out ReadOnlySpan<byte> moof)
    {
        moof = default;
        var offset = 0;
        while (offset + 8 <= data.Length)
        {
            if (!TryReadBoxHeader(data, offset, out var boxSize, out var type, out var headerSize))
                return false;

            if (type.SequenceEqual(Moof))
            {
                moof = data.Slice(offset, (int)boxSize);
                return true;
            }

            offset += (int)boxSize;
            if (boxSize == 0)
                break;
        }

        return false;
    }

    /// <summary>
    /// Rejects trun sample_duration/sample_size/CTS values with the high bit set (ExoPlayer
    /// "Top bit not zero"), and tfhd absolute base_data_offset (non-CMAF).
    /// </summary>
    private static bool TryValidateAndDescribeTrun(
        ReadOnlySpan<byte> moofBox,
        out string summary,
        out string failReason)
    {
        summary = "no-traf";
        failReason = "ok";

        // Skip moof header
        if (!TryReadBoxHeader(moofBox, 0, out _, out _, out var moofHeader))
        {
            failReason = "moof-header-invalid";
            return false;
        }

        var offset = moofHeader;
        while (offset + 8 <= moofBox.Length)
        {
            if (!TryReadBoxHeader(moofBox, offset, out var boxSize, out var type, out var headerSize))
            {
                failReason = "moof-child-invalid";
                return false;
            }

            if (type.SequenceEqual(Traf))
            {
                var traf = moofBox.Slice(offset, (int)boxSize);
                return TryValidateTraf(traf, out summary, out failReason);
            }

            offset += (int)boxSize;
        }

        failReason = "traf-missing";
        return false;
    }

    private static bool TryValidateTraf(
        ReadOnlySpan<byte> trafBox,
        out string summary,
        out string failReason)
    {
        summary = "traf-empty";
        failReason = "ok";

        if (!TryReadBoxHeader(trafBox, 0, out _, out _, out var trafHeader))
        {
            failReason = "traf-header-invalid";
            return false;
        }

        var sawTrun = false;
        var sawTfdt = false;
        var sb = new StringBuilder();
        var offset = trafHeader;
        while (offset + 8 <= trafBox.Length)
        {
            if (!TryReadBoxHeader(trafBox, offset, out var boxSize, out var type, out var headerSize))
            {
                failReason = "traf-child-invalid";
                return false;
            }

            var payload = trafBox.Slice(offset + headerSize, (int)boxSize - headerSize);

            if (type.SequenceEqual(Tfhd))
            {
                if (!TryParseTfhd(payload, out var tfhdSummary, out var tfhdFail))
                {
                    failReason = tfhdFail;
                    summary = tfhdSummary;
                    return false;
                }

                if (sb.Length > 0)
                    sb.Append(';');
                sb.Append(tfhdSummary);
            }
            else if (type.SequenceEqual(Tfdt))
            {
                sawTfdt = true;
                if (!TryParseTfdt(payload, out var tfdtSummary, out var tfdtFail))
                {
                    failReason = tfdtFail;
                    summary = tfdtSummary;
                    return false;
                }

                if (sb.Length > 0)
                    sb.Append(';');
                sb.Append(tfdtSummary);
            }
            else if (type.SequenceEqual(Trun))
            {
                sawTrun = true;
                if (!TryParseTrun(payload, out var trunSummary, out var trunFail))
                {
                    failReason = trunFail;
                    summary = trunSummary;
                    return false;
                }

                if (sb.Length > 0)
                    sb.Append(';');
                sb.Append(trunSummary);
            }

            offset += (int)boxSize;
        }

        if (!sawTrun)
        {
            failReason = "trun-missing";
            return false;
        }

        if (!sawTfdt)
        {
            failReason = "tfdt-missing";
            return false;
        }

        summary = sb.ToString();
        return true;
    }

    private static bool TryParseTfdt(
        ReadOnlySpan<byte> payload,
        out string summary,
        out string failReason)
    {
        summary = "tfdt";
        failReason = "ok";

        if (payload.Length < 8)
        {
            failReason = "tfdt-too-short";
            return false;
        }

        var full = BinaryPrimitives.ReadUInt32BigEndian(payload);
        var version = (byte)(full >> 24);

        long baseDecodeTime;
        if (version == 1)
        {
            if (payload.Length < 12)
            {
                failReason = "tfdt-v1-too-short";
                return false;
            }

            var high = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
            var low = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(8, 4));

            // ExoPlayer readUnsignedLongToLong rejects any value with the high bit set.
            // AAC priming + avoid_negative_ts disabled writes 0xFFFFFFFFFFFFFC00 (-1024),
            // which surfaces as "Top bit not zero: -1024".
            if ((high & 0x8000_0000u) != 0)
            {
                var asSigned = unchecked((int)low);
                failReason = "tfdt-negative-or-top-bit";
                summary =
                    "tfdt v1 base=0x"
                    + high.ToString("X8", CultureInfo.InvariantCulture)
                    + low.ToString("X8", CultureInfo.InvariantCulture)
                    + " signedLow="
                    + asSigned.ToString(CultureInfo.InvariantCulture)
                    + " (ExoPlayer Top bit not zero risk)";
                return false;
            }

            baseDecodeTime = ((long)high << 32) | low;
        }
        else
        {
            var base32 = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
            if ((base32 & 0x8000_0000u) != 0)
            {
                failReason = "tfdt-negative-or-top-bit";
                summary =
                    "tfdt v0 base=0x"
                    + base32.ToString("X8", CultureInfo.InvariantCulture)
                    + " (ExoPlayer Top bit not zero risk)";
                return false;
            }

            baseDecodeTime = base32;
        }

        summary =
            "tfdt v="
            + version
            + " base="
            + baseDecodeTime.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static bool TryParseTfhd(
        ReadOnlySpan<byte> payload,
        out string summary,
        out string failReason)
    {
        summary = "tfhd";
        failReason = "ok";

        if (payload.Length < 8)
        {
            failReason = "tfhd-too-short";
            return false;
        }

        var full = BinaryPrimitives.ReadUInt32BigEndian(payload);
        var flags = full & 0x00FF_FFFF;
        var trackId = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
        var hasBaseDataOffset = (flags & 0x1) != 0;
        var defaultBaseIsMoof = (flags & 0x2_0000) != 0;

        summary =
            "tfhd track="
            + trackId
            + " flags=0x"
            + flags.ToString("X6", CultureInfo.InvariantCulture)
            + " dbim="
            + (defaultBaseIsMoof ? "1" : "0")
            + " bdo="
            + (hasBaseDataOffset ? "1" : "0");

        // Absolute base_data_offset ties the fragment to a file offset ExoPlayer does not have
        // when playing discrete HLS .m4s files.
        if (hasBaseDataOffset)
        {
            failReason = "tfhd-has-base-data-offset";
            return false;
        }

        return true;
    }

    private static bool TryParseTrun(
        ReadOnlySpan<byte> payload,
        out string summary,
        out string failReason)
    {
        summary = "trun";
        failReason = "ok";

        if (payload.Length < 8)
        {
            failReason = "trun-too-short";
            return false;
        }

        var full = BinaryPrimitives.ReadUInt32BigEndian(payload);
        var version = (byte)(full >> 24);
        var flags = full & 0x00FF_FFFF;
        var sampleCount = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));

        if ((sampleCount & 0x8000_0000u) != 0)
        {
            failReason = "trun-sample-count-top-bit";
            summary = "trun count=0x" + sampleCount.ToString("X8", CultureInfo.InvariantCulture);
            return false;
        }

        var cursor = 8;
        int? dataOffset = null;
        if ((flags & 0x1) != 0)
        {
            if (cursor + 4 > payload.Length)
            {
                failReason = "trun-data-offset-truncated";
                return false;
            }

            dataOffset = BinaryPrimitives.ReadInt32BigEndian(payload.Slice(cursor, 4));
            cursor += 4;
        }

        if ((flags & 0x4) != 0)
        {
            if (cursor + 4 > payload.Length)
            {
                failReason = "trun-first-flags-truncated";
                return false;
            }

            cursor += 4;
        }

        var hasDuration = (flags & 0x100) != 0;
        var hasSize = (flags & 0x200) != 0;
        var hasSampleFlags = (flags & 0x400) != 0;
        var hasCts = (flags & 0x800) != 0;

        var firstSizes = new List<uint>(8);
        var firstCts = new List<int>(8);
        var negCts = 0;

        for (var i = 0; i < sampleCount; i++)
        {
            if (hasDuration)
            {
                if (cursor + 4 > payload.Length)
                {
                    failReason = "trun-duration-truncated";
                    return false;
                }

                var duration = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(cursor, 4));
                cursor += 4;
                if ((duration & 0x8000_0000u) != 0)
                {
                    failReason = "trun-sample-duration-top-bit";
                    summary = "trun dur[" + i + "]=0x" + duration.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }
            }

            if (hasSize)
            {
                if (cursor + 4 > payload.Length)
                {
                    failReason = "trun-size-truncated";
                    return false;
                }

                var size = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(cursor, 4));
                cursor += 4;
                if ((size & 0x8000_0000u) != 0)
                {
                    failReason = "trun-sample-size-top-bit";
                    summary = "trun size[" + i + "]=0x" + size.ToString("X8", CultureInfo.InvariantCulture);
                    return false;
                }

                if (firstSizes.Count < 8)
                    firstSizes.Add(size);
            }

            if (hasSampleFlags)
            {
                if (cursor + 4 > payload.Length)
                {
                    failReason = "trun-flags-truncated";
                    return false;
                }

                cursor += 4;
            }

            if (hasCts)
            {
                if (cursor + 4 > payload.Length)
                {
                    failReason = "trun-cts-truncated";
                    return false;
                }

                var ctsRaw = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(cursor, 4));
                cursor += 4;

                // trun v0: CTS is unsigned; high bit is exactly ExoPlayer "Top bit not zero".
                // trun v1: CTS is signed; negative values (e.g. -1024 from negative_cts_offsets)
                // are what we observed failing Android playback for this title.
                if (version == 0)
                {
                    if ((ctsRaw & 0x8000_0000u) != 0)
                    {
                        failReason = "trun-cts-top-bit";
                        summary = "trun cts[" + i + "]=0x" + ctsRaw.ToString("X8", CultureInfo.InvariantCulture);
                        return false;
                    }

                    if (firstCts.Count < 8)
                        firstCts.Add((int)ctsRaw);
                }
                else
                {
                    var cts = unchecked((int)ctsRaw);
                    if (cts < 0)
                        negCts++;

                    if (firstCts.Count < 8)
                        firstCts.Add(cts);

                    if (cts < 0)
                    {
                        failReason = "trun-cts-negative";
                        summary =
                            "trun v1 cts["
                            + i
                            + "]="
                            + cts.ToString(CultureInfo.InvariantCulture)
                            + " (ExoPlayer Top bit not zero risk)";
                        return false;
                    }
                }
            }
        }

        summary = new StringBuilder(96)
            .Append("trun v=")
            .Append(version)
            .Append(" flags=0x")
            .Append(flags.ToString("X6", CultureInfo.InvariantCulture))
            .Append(" count=")
            .Append(sampleCount)
            .Append(" data_offset=")
            .Append(dataOffset?.ToString(CultureInfo.InvariantCulture) ?? "-")
            .Append(" sizes=[")
            .Append(string.Join(',', firstSizes))
            .Append(']')
            .Append(" cts=[")
            .Append(string.Join(',', firstCts))
            .Append(']')
            .Append(" negCts=")
            .Append(negCts)
            .ToString();

        return true;
    }

    private static bool TryReadBoxHeader(
        ReadOnlySpan<byte> data,
        int offset,
        out long boxSize,
        out ReadOnlySpan<byte> type,
        out int headerSize)
    {
        boxSize = 0;
        type = default;
        headerSize = 8;

        if (offset + 8 > data.Length)
            return false;

        var sizeField = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
        type = data.Slice(offset + 4, 4);

        if (sizeField == 1)
        {
            if (offset + 16 > data.Length)
                return false;

            var largeSize = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 8, 8));
            if (largeSize > long.MaxValue)
                return false;

            boxSize = (long)largeSize;
            headerSize = 16;
        }
        else if (sizeField == 0)
        {
            boxSize = data.Length - offset;
        }
        else
        {
            if ((sizeField & 0x8000_0000u) != 0)
                return false;

            boxSize = sizeField;
        }

        return boxSize >= headerSize && offset + boxSize <= data.Length;
    }

    private static bool IsInitSegmentPath(string segmentPath) =>
        string.Equals(Path.GetFileName(segmentPath), InitSegmentFileName, StringComparison.OrdinalIgnoreCase);

    private static bool IsSizeZeroSnapshotSafe(string segmentPath, int length)
    {
        // If the next numbered media segment has content, ffmpeg has closed this one.
        if (TryGetNextMediaSegmentPath(segmentPath, out var nextPath) && HasNonEmptyContent(nextPath))
            return true;

        var now = Stopwatch.GetTimestamp();
        var key = segmentPath;

        if (SizeZeroStability.TryGetValue(key, out var prev) && prev.Length == length)
        {
            var elapsedMs = Stopwatch.GetElapsedTime(prev.Timestamp, now).TotalMilliseconds;
            return elapsedMs >= SizeZeroStableMs;
        }

        SizeZeroStability[key] = (length, now);
        return false;
    }

    private static void ClearSizeZeroStability(string segmentPath) =>
        SizeZeroStability.TryRemove(segmentPath, out _);

    private static bool TryGetNextMediaSegmentPath(string segmentPath, out string nextPath)
    {
        nextPath = string.Empty;
        var fileName = Path.GetFileNameWithoutExtension(segmentPath);
        if (!int.TryParse(fileName, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            return false;

        nextPath = Path.Combine(
            Path.GetDirectoryName(segmentPath) ?? string.Empty,
            $"{index + 1}.m4s");
        return true;
    }

    private static bool TryWalkCompleteBoxes(
        ReadOnlySpan<byte> data,
        out bool sawFtyp,
        out bool sawMoov,
        out bool sawMoof,
        out bool sawMdat,
        out bool hasSizeZeroBox,
        out string description)
    {
        sawFtyp = sawMoov = sawMoof = sawMdat = false;
        hasSizeZeroBox = false;
        var offset = 0;
        var sb = new StringBuilder();

        while (offset < data.Length)
        {
            if (offset + 8 > data.Length)
            {
                description = sb.ToString();
                return false;
            }

            var sizeField = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
            var type = data.Slice(offset + 4, 4);
            var typeName = Encoding.ASCII.GetString(type);

            long boxSize;
            var headerSize = 8;
            if (sizeField == 1)
            {
                if (offset + 16 > data.Length)
                {
                    description = sb.ToString();
                    return false;
                }

                var largeSize = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 8, 8));
                if (largeSize > long.MaxValue)
                {
                    description = sb.ToString();
                    return false;
                }

                boxSize = (long)largeSize;
                headerSize = 16;
            }
            else if (sizeField == 0)
            {
                hasSizeZeroBox = true;
                boxSize = data.Length - offset;
            }
            else
            {
                boxSize = sizeField;
            }

            // High bit set on a 32-bit size is invalid in practice for our segments and is
            // exactly what ExoPlayer rejects ("Top bit not zero").
            if (sizeField is not (0 or 1) && (sizeField & 0x8000_0000u) != 0)
            {
                description = sb.ToString();
                return false;
            }

            if (boxSize < headerSize || offset + boxSize > data.Length)
            {
                description = sb.ToString();
                return false;
            }

            if (sb.Length > 0)
                sb.Append(',');
            sb.Append(typeName);
            sb.Append(':');
            sb.Append(sizeField == 0 ? "EOF" : boxSize.ToString(CultureInfo.InvariantCulture));

            if (type.SequenceEqual(Ftyp))
                sawFtyp = true;
            else if (type.SequenceEqual(Moov))
                sawMoov = true;
            else if (type.SequenceEqual(Moof))
                sawMoof = true;
            else if (type.SequenceEqual(Mdat))
                sawMdat = true;

            offset += (int)boxSize;
            if (sizeField == 0)
                break;
        }

        description = sb.ToString();
        return offset == data.Length;
    }
}
