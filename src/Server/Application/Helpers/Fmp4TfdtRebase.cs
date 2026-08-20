using System.Buffers.Binary;
using System.Globalization;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Fixes fMP4 decode timelines after lazy ffmpeg windows.
/// Remap tfdt on a window reset. Video copy uses a 1s threshold so the ~83ms
/// source CTS is kept. Video encode uses 20ms and can subtract the first
/// sample CTS so presentation (not only decode) lands on the playlist.
/// </summary>
internal static class Fmp4TfdtRebase
{
    /// <summary>
    /// Source composition (~83ms) stays under this. NVENC-style encoder delay is above.
    /// </summary>
    private const int EncoderPresentationAlignThresholdMs = 250;

    private static ReadOnlySpan<byte> Moov => "moov"u8;
    private static ReadOnlySpan<byte> Trak => "trak"u8;
    private static ReadOnlySpan<byte> Mdia => "mdia"u8;
    private static ReadOnlySpan<byte> Mdhd => "mdhd"u8;
    private static ReadOnlySpan<byte> Moof => "moof"u8;
    private static ReadOnlySpan<byte> Traf => "traf"u8;
    private static ReadOnlySpan<byte> Tfdt => "tfdt"u8;
    private static ReadOnlySpan<byte> Trun => "trun"u8;

    public static bool TryRebaseMediaSegment(
        byte[] segmentBytes,
        string initSegmentPath,
        long segmentStartTimestampMs,
        out byte[] rebasedBytes,
        out string detail,
        int toleranceMs = Hls.TfdtWindowResetThresholdMs,
        bool alignPresentationTime = false)
    {
        rebasedBytes = segmentBytes;
        detail = "skipped";

        if (segmentBytes.Length == 0 || segmentStartTimestampMs < 0)
            return false;

        if (!TryReadTimescaleFromInit(initSegmentPath, out var timescale) || timescale == 0)
        {
            detail = "no-timescale";
            return false;
        }

        var tfdtPayloadOffsets = new List<int>();
        CollectTfdtPayloadOffsets(segmentBytes, 0, segmentBytes.Length, tfdtPayloadOffsets);
        if (tfdtPayloadOffsets.Count == 0)
        {
            detail = "no-tfdt";
            return false;
        }

        if (!TryReadTfdtBase(segmentBytes.AsSpan(tfdtPayloadOffsets[0]), out var currentBase))
        {
            detail = "no-tfdt";
            return false;
        }

        var expectedBase = (ulong)Math.Round(segmentStartTimestampMs / 1000.0 * timescale);
        var compositionOffset = 0;
        var hasCompositionOffset = TryReadFirstCompositionOffset(
            segmentBytes,
            tfdtPayloadOffsets,
            out compositionOffset);
        var compositionMs = hasCompositionOffset && compositionOffset != 0
            ? compositionOffset * 1000.0 / timescale
            : 0;

        // Signed delta before presentation adjust: detect encoder delay vs source composition.
        var deltaBeforeAlign = (long)expectedBase - (long)currentBase;
        var driftMs = Math.Abs(deltaBeforeAlign) * 1000.0 / timescale;

        // Encode-only serve path: flatten NVENC delay, not the ~83ms source composition
        // that audio copy keeps. Rebasing composition onto the playlist desyncs Web MSE.
        if (alignPresentationTime
            && driftMs < EncoderPresentationAlignThresholdMs
            && compositionMs < EncoderPresentationAlignThresholdMs)
        {
            alignPresentationTime = false;
            toleranceMs = Math.Max(toleranceMs, Hls.TfdtWindowResetThresholdMs);
        }

        var compositionNote = string.Empty;
        if (alignPresentationTime
            && hasCompositionOffset
            && compositionOffset != 0)
        {
            var presentationBase = (long)expectedBase - compositionOffset;
            if (presentationBase < 0)
                presentationBase = 0;
            expectedBase = (ulong)presentationBase;
            compositionNote = ", cts="
                + compositionOffset.ToString(CultureInfo.InvariantCulture);
        }

        // Signed delta: must correct both under- and over-shot bases. The old
        // "currentBase + timescale >= expectedBase" check skipped stale segments that
        // were rebased onto a wrong (too high) absolute timeline - classic A/V desync
        // after an audio equal-length fallback.
        var delta = (long)expectedBase - (long)currentBase;
        var clampedToleranceMs = Math.Max(toleranceMs, 1);
        var tolerance = (long)Math.Max(
            Math.Round(timescale * (clampedToleranceMs / 1000.0)),
            1);
        if (Math.Abs(delta) <= tolerance)
        {
            detail = "already-absolute base="
                + currentBase.ToString(CultureInfo.InvariantCulture)
                + " expected="
                + expectedBase.ToString(CultureInfo.InvariantCulture);
            return false;
        }

        var copy = (byte[])segmentBytes.Clone();
        var patched = 0;
        foreach (var payloadOffset in tfdtPayloadOffsets)
        {
            var payload = copy.AsSpan(payloadOffset);
            if (!TryReadTfdtBase(payload, out var baseValue))
                continue;
            var next = (long)baseValue + delta;
            if (next < 0)
                continue;
            if (!TryWriteTfdtBase(payload, (ulong)next))
                continue;
            patched++;
        }

        if (patched == 0)
        {
            detail = "tfdt-patch-failed";
            return false;
        }

        rebasedBytes = copy;
        detail = "rebased tfdt "
            + (delta >= 0 ? "+" : string.Empty)
            + delta.ToString(CultureInfo.InvariantCulture)
            + " (timescale="
            + timescale.ToString(CultureInfo.InvariantCulture)
            + ", from="
            + currentBase.ToString(CultureInfo.InvariantCulture)
            + ", to~="
            + expectedBase.ToString(CultureInfo.InvariantCulture)
            + ", boxes="
            + patched.ToString(CultureInfo.InvariantCulture)
            + compositionNote
            + ")";
        return true;
    }

    private static bool TryReadFirstCompositionOffset(
        ReadOnlySpan<byte> segmentBytes,
        IReadOnlyList<int> tfdtPayloadOffsets,
        out int compositionOffset)
    {
        compositionOffset = 0;
        _ = tfdtPayloadOffsets;
        return TryFindTrunCompositionOffset(segmentBytes, 0, segmentBytes.Length, out compositionOffset);
    }

    private static bool TryFindTrunCompositionOffset(
        ReadOnlySpan<byte> data,
        int start,
        int end,
        out int compositionOffset)
    {
        compositionOffset = 0;
        var offset = start;
        while (offset + 8 <= end)
        {
            if (!TryReadBoxHeader(data, offset, end, out var boxSize, out var type, out var headerSize))
                return false;

            var payloadStart = offset + headerSize;
            var payloadEnd = offset + (int)boxSize;
            if (payloadEnd > end || payloadStart > payloadEnd)
                return false;

            if (type.SequenceEqual(Trun))
                return TryParseFirstSampleCompositionOffset(
                    data.Slice(payloadStart, payloadEnd - payloadStart),
                    out compositionOffset);

            if (type.SequenceEqual(Moof) || type.SequenceEqual(Traf))
            {
                if (TryFindTrunCompositionOffset(data, payloadStart, payloadEnd, out compositionOffset))
                    return true;
            }

            offset = payloadEnd;
        }

        return false;
    }

    private static bool TryParseFirstSampleCompositionOffset(ReadOnlySpan<byte> payload, out int compositionOffset)
    {
        compositionOffset = 0;
        if (payload.Length < 8)
            return false;

        var full = BinaryPrimitives.ReadUInt32BigEndian(payload);
        var version = (byte)(full >> 24);
        var flags = full & 0x00FF_FFFF;
        const uint dataOffsetPresent = 0x000001;
        const uint firstSampleFlagsPresent = 0x000004;
        const uint sampleDurationPresent = 0x000100;
        const uint sampleSizePresent = 0x000200;
        const uint sampleFlagsPresent = 0x000400;
        const uint sampleCompositionPresent = 0x000800;
        if ((flags & sampleCompositionPresent) == 0)
            return false;

        var cursor = 8;
        if ((flags & dataOffsetPresent) != 0)
            cursor += 4;
        if ((flags & firstSampleFlagsPresent) != 0)
            cursor += 4;
        if ((flags & sampleDurationPresent) != 0)
            cursor += 4;
        if ((flags & sampleSizePresent) != 0)
            cursor += 4;
        if ((flags & sampleFlagsPresent) != 0)
            cursor += 4;
        if (cursor + 4 > payload.Length)
            return false;

        var raw = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(cursor, 4));
        compositionOffset = version == 1 ? unchecked((int)raw) : (int)raw;
        return true;
    }

    private static bool TryReadTimescaleFromInit(string initSegmentPath, out uint timescale)
    {
        timescale = 0;
        if (!File.Exists(initSegmentPath))
            return false;

        try
        {
            return TryFindMdhdTimescale(File.ReadAllBytes(initSegmentPath), out timescale);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryFindMdhdTimescale(ReadOnlySpan<byte> data, out uint timescale)
    {
        timescale = 0;
        var offset = 0;
        while (offset + 8 <= data.Length)
        {
            if (!TryReadBoxHeader(data, offset, data.Length, out var boxSize, out var type, out var headerSize))
                return false;

            var payloadStart = offset + headerSize;
            var payloadEnd = offset + (int)boxSize;
            if (payloadEnd > data.Length || payloadStart > payloadEnd)
                return false;

            if (type.SequenceEqual(Mdhd))
                return TryParseMdhdTimescale(data.Slice(payloadStart, payloadEnd - payloadStart), out timescale);

            if (type.SequenceEqual(Moov) || type.SequenceEqual(Trak) || type.SequenceEqual(Mdia))
            {
                if (TryFindMdhdTimescale(data.Slice(payloadStart, payloadEnd - payloadStart), out timescale))
                    return true;
            }

            offset = payloadEnd;
        }

        return false;
    }

    private static bool TryParseMdhdTimescale(ReadOnlySpan<byte> payload, out uint timescale)
    {
        timescale = 0;
        if (payload.Length < 8)
            return false;

        var version = payload[0];
        if (version == 1)
        {
            if (payload.Length < 24)
                return false;
            timescale = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(20, 4));
            return timescale > 0;
        }

        if (payload.Length < 16)
            return false;
        timescale = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(12, 4));
        return timescale > 0;
    }

    private static void CollectTfdtPayloadOffsets(ReadOnlySpan<byte> data, int start, int end, List<int> offsets)
    {
        var offset = start;
        while (offset + 8 <= end)
        {
            if (!TryReadBoxHeader(data, offset, end, out var boxSize, out var type, out var headerSize))
                return;

            var payloadStart = offset + headerSize;
            var payloadEnd = offset + (int)boxSize;
            if (payloadEnd > end || payloadStart > payloadEnd)
                return;

            if (type.SequenceEqual(Tfdt))
                offsets.Add(payloadStart);
            else if (type.SequenceEqual(Moof) || type.SequenceEqual(Traf))
                CollectTfdtPayloadOffsets(data, payloadStart, payloadEnd, offsets);

            offset = payloadEnd;
        }
    }

    private static bool TryReadTfdtBase(ReadOnlySpan<byte> payload, out ulong baseDecodeTime)
    {
        baseDecodeTime = 0;
        if (payload.Length < 8)
            return false;

        var version = (byte)(BinaryPrimitives.ReadUInt32BigEndian(payload) >> 24);
        if (version == 1)
        {
            if (payload.Length < 12)
                return false;
            var high = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
            var low = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(8, 4));
            if ((high & 0x8000_0000u) != 0)
                return false;
            baseDecodeTime = ((ulong)high << 32) | low;
            return true;
        }

        var base32 = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(4, 4));
        if ((base32 & 0x8000_0000u) != 0)
            return false;
        baseDecodeTime = base32;
        return true;
    }

    private static bool TryWriteTfdtBase(Span<byte> payload, ulong baseDecodeTime)
    {
        if (payload.Length < 8)
            return false;

        var version = (byte)(BinaryPrimitives.ReadUInt32BigEndian(payload) >> 24);
        if (version == 1)
        {
            if (payload.Length < 12)
                return false;
            BinaryPrimitives.WriteUInt32BigEndian(payload.Slice(4, 4), (uint)(baseDecodeTime >> 32));
            BinaryPrimitives.WriteUInt32BigEndian(payload.Slice(8, 4), (uint)baseDecodeTime);
            return true;
        }

        if (baseDecodeTime > uint.MaxValue)
            return false;

        BinaryPrimitives.WriteUInt32BigEndian(payload.Slice(4, 4), (uint)baseDecodeTime);
        return true;
    }

    private static bool TryReadBoxHeader(
        ReadOnlySpan<byte> data,
        int offset,
        int end,
        out long boxSize,
        out ReadOnlySpan<byte> type,
        out int headerSize)
    {
        boxSize = 0;
        type = default;
        headerSize = 8;

        if (offset + 8 > end)
            return false;

        var sizeField = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset, 4));
        type = data.Slice(offset + 4, 4);

        if (sizeField == 1)
        {
            if (offset + 16 > end)
                return false;
            var largeSize = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 8, 8));
            if (largeSize > long.MaxValue)
                return false;
            boxSize = (long)largeSize;
            headerSize = 16;
        }
        else if (sizeField == 0)
        {
            boxSize = end - offset;
        }
        else
        {
            if ((sizeField & 0x8000_0000u) != 0)
                return false;
            boxSize = sizeField;
        }

        return boxSize >= headerSize && offset + boxSize <= end;
    }
}
