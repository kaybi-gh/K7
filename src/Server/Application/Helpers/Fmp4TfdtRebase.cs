using System.Buffers.Binary;
using System.Globalization;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Fixes fMP4 decode timelines after lazy ffmpeg windows.
/// With -start_at_zero, each restart resets tfdt to ~0; players then stall on
/// timeline discontinuities. Shift every tfdt base to the absolute playlist time.
/// </summary>
internal static class Fmp4TfdtRebase
{
    private static ReadOnlySpan<byte> Moov => "moov"u8;
    private static ReadOnlySpan<byte> Trak => "trak"u8;
    private static ReadOnlySpan<byte> Mdia => "mdia"u8;
    private static ReadOnlySpan<byte> Mdhd => "mdhd"u8;
    private static ReadOnlySpan<byte> Moof => "moof"u8;
    private static ReadOnlySpan<byte> Traf => "traf"u8;
    private static ReadOnlySpan<byte> Tfdt => "tfdt"u8;

    public static bool TryRebaseMediaSegment(
        byte[] segmentBytes,
        string initSegmentPath,
        long segmentStartTimestampMs,
        out byte[] rebasedBytes,
        out string detail)
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

        // Signed delta: must correct both under- and over-shot bases. The old
        // "currentBase + timescale >= expectedBase" check skipped stale segments that
        // were rebased onto a wrong (too high) absolute timeline - classic A/V desync
        // after an audio equal-length fallback.
        var delta = (long)expectedBase - (long)currentBase;
        var tolerance = (long)Math.Max(timescale / 2, 1);
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
            + ")";
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
