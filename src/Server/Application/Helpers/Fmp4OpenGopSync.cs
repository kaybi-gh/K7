using System.Buffers.Binary;

namespace K7.Server.Application.Helpers;

/// <summary>
/// Open-GOP HEVC (Heroes and similar) uses CRA at almost every playlist keyframe
/// and a real IDR only at t=0. ffmpeg marks that CRA as a sync sample. ExoPlayer
/// then flushes the decoder at each remux .m4s and the picture cuts. Demote the
/// first-sample sync flag when the first VCL NAL is CRA so linear decode continues.
/// Leave true IDR segments alone (playback start / rare closed-GOP cuts).
/// </summary>
internal static class Fmp4OpenGopSync
{
    private const int HevcIdrWRadl = 19;
    private const int HevcIdrNLp = 20;
    private const int HevcCra = 21;
    private const uint SampleIsNonSyncFlag = 0x0001_0000;

    private static ReadOnlySpan<byte> Moof => "moof"u8;
    private static ReadOnlySpan<byte> Traf => "traf"u8;
    private static ReadOnlySpan<byte> Tfhd => "tfhd"u8;
    private static ReadOnlySpan<byte> Trun => "trun"u8;
    private static ReadOnlySpan<byte> Mdat => "mdat"u8;

    public static bool TryDemoteCraFirstSample(
        byte[] segmentBytes,
        out byte[] patchedBytes,
        out string detail)
    {
        patchedBytes = segmentBytes;
        detail = "skipped";

        if (segmentBytes.Length < 16)
            return false;

        if (!TryReadFirstHevcVclNalType(segmentBytes, out var nalType))
        {
            detail = "no-vcl";
            return false;
        }

        if (nalType is HevcIdrWRadl or HevcIdrNLp)
        {
            detail = "idr";
            return false;
        }

        if (nalType != HevcCra)
        {
            detail = "not-cra nal=" + nalType.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return false;
        }

        if (!TryFindFirstSampleFlagsOffset(segmentBytes, out var flagsOffset, out var currentFlags))
        {
            detail = "no-first-sample-flags";
            return false;
        }

        if ((currentFlags & SampleIsNonSyncFlag) != 0)
        {
            detail = "already-nonsync";
            return false;
        }

        var defaultFlags = TryReadDefaultSampleFlags(segmentBytes, out var parsed)
            ? parsed
            : 0x0101_0000u;
        defaultFlags |= SampleIsNonSyncFlag;

        var copy = (byte[])segmentBytes.Clone();
        BinaryPrimitives.WriteUInt32BigEndian(copy.AsSpan(flagsOffset, 4), defaultFlags);
        patchedBytes = copy;
        detail = "demoted-cra-sync";
        return true;
    }

    public static bool TryPersistDemotedCra(string segmentPath)
    {
        try
        {
            if (!File.Exists(segmentPath))
                return false;

            var bytes = File.ReadAllBytes(segmentPath);
            if (!TryDemoteCraFirstSample(bytes, out var patched, out _))
                return false;

            File.WriteAllBytes(segmentPath, patched);
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

    private static bool TryReadFirstHevcVclNalType(ReadOnlySpan<byte> data, out int nalType)
    {
        nalType = -1;
        var offset = 0;
        while (offset + 8 <= data.Length)
        {
            if (!TryReadBoxHeader(data, offset, data.Length, out var boxSize, out var type, out var headerSize))
                return false;

            var payloadStart = offset + headerSize;
            var payloadEnd = offset + (int)boxSize;
            if (type.SequenceEqual(Mdat))
                return TryReadFirstVclFromLengthPrefixed(data.Slice(payloadStart, payloadEnd - payloadStart), out nalType);

            offset = payloadEnd;
        }

        return false;
    }

    private static bool TryReadFirstVclFromLengthPrefixed(ReadOnlySpan<byte> mdat, out int nalType)
    {
        nalType = -1;
        var i = 0;
        while (i + 6 <= mdat.Length)
        {
            var nalSize = BinaryPrimitives.ReadUInt32BigEndian(mdat.Slice(i, 4));
            i += 4;
            if (nalSize < 2 || i + (int)nalSize > mdat.Length)
                return false;

            nalType = (mdat[i] >> 1) & 0x3F;
            i += (int)nalSize;
            if (nalType is >= 32 and <= 35 or >= 39 and <= 41)
                continue;

            return true;
        }

        return false;
    }

    private static bool TryFindFirstSampleFlagsOffset(
        ReadOnlySpan<byte> data,
        out int flagsOffset,
        out uint flags)
    {
        flagsOffset = 0;
        flags = 0;
        return TryWalkMoofForFirstSampleFlags(data, 0, data.Length, out flagsOffset, out flags);
    }

    private static bool TryWalkMoofForFirstSampleFlags(
        ReadOnlySpan<byte> data,
        int start,
        int end,
        out int flagsOffset,
        out uint flags)
    {
        flagsOffset = 0;
        flags = 0;
        var offset = start;
        while (offset + 8 <= end)
        {
            if (!TryReadBoxHeader(data, offset, end, out var boxSize, out var type, out var headerSize))
                return false;

            var payloadStart = offset + headerSize;
            var payloadEnd = offset + (int)boxSize;
            if (type.SequenceEqual(Trun))
                return TryParseTrunFirstSampleFlags(data, payloadStart, payloadEnd, out flagsOffset, out flags);

            if (type.SequenceEqual(Moof) || type.SequenceEqual(Traf))
            {
                if (TryWalkMoofForFirstSampleFlags(data, payloadStart, payloadEnd, out flagsOffset, out flags))
                    return true;
            }

            offset = payloadEnd;
        }

        return false;
    }

    private static bool TryParseTrunFirstSampleFlags(
        ReadOnlySpan<byte> data,
        int payloadStart,
        int payloadEnd,
        out int flagsOffset,
        out uint flags)
    {
        flagsOffset = 0;
        flags = 0;
        if (payloadEnd - payloadStart < 8)
            return false;

        var trunFlags = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(payloadStart, 4)) & 0x00FF_FFFF;
        var cursor = payloadStart + 8;
        if ((trunFlags & 0x000001) != 0)
            cursor += 4;

        if ((trunFlags & 0x000004) == 0)
            return false;

        if (cursor + 4 > payloadEnd)
            return false;

        flagsOffset = cursor;
        flags = BinaryPrimitives.ReadUInt32BigEndian(data.Slice(cursor, 4));
        return true;
    }

    private static bool TryReadDefaultSampleFlags(ReadOnlySpan<byte> data, out uint defaultFlags)
    {
        defaultFlags = 0;
        return TryWalkTfhd(data, 0, data.Length, out defaultFlags);
    }

    private static bool TryWalkTfhd(ReadOnlySpan<byte> data, int start, int end, out uint defaultFlags)
    {
        defaultFlags = 0;
        var offset = start;
        while (offset + 8 <= end)
        {
            if (!TryReadBoxHeader(data, offset, end, out var boxSize, out var type, out var headerSize))
                return false;

            var payloadStart = offset + headerSize;
            var payloadEnd = offset + (int)boxSize;
            if (type.SequenceEqual(Tfhd))
                return TryParseTfhdDefaultFlags(data.Slice(payloadStart, payloadEnd - payloadStart), out defaultFlags);

            if (type.SequenceEqual(Moof) || type.SequenceEqual(Traf))
            {
                if (TryWalkTfhd(data, payloadStart, payloadEnd, out defaultFlags))
                    return true;
            }

            offset = payloadEnd;
        }

        return false;
    }

    private static bool TryParseTfhdDefaultFlags(ReadOnlySpan<byte> payload, out uint defaultFlags)
    {
        defaultFlags = 0;
        if (payload.Length < 8)
            return false;

        var flags = BinaryPrimitives.ReadUInt32BigEndian(payload) & 0x00FF_FFFF;
        var cursor = 8;
        if ((flags & 0x000001) != 0)
            cursor += 8;
        if ((flags & 0x000002) != 0)
            cursor += 4;
        if ((flags & 0x000008) != 0)
            cursor += 4;
        if ((flags & 0x000010) != 0)
            cursor += 4;
        if ((flags & 0x000020) == 0 || cursor + 4 > payload.Length)
            return false;

        defaultFlags = BinaryPrimitives.ReadUInt32BigEndian(payload.Slice(cursor, 4));
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
            var large = BinaryPrimitives.ReadUInt64BigEndian(data.Slice(offset + 8, 8));
            if (large > long.MaxValue)
                return false;
            boxSize = (long)large;
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
