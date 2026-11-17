namespace K7.Clients.ComponentTests.Helpers;

/// <summary>
/// Builds small .sup streams for the PGS tests: "PG" + PTS + DTS + type + size + payload.
/// </summary>
internal sealed class PgsStreamBuilder
{
    public const byte EpochStart = 0x80;
    public const byte NormalCase = 0x00;

    /// <summary>2x2 object: first line two pixels of colour 1, second line a run of two of colour 2.</summary>
    public static readonly byte[] TwoByTwoRle = [1, 1, 0, 0, 0, 0x82, 2, 0, 0];

    private readonly MemoryStream _stream = new();

    public byte[] ToArray() => _stream.ToArray();

    public PgsStreamBuilder Composition(
        double seconds,
        int width,
        int height,
        byte state,
        byte paletteId,
        params (int ObjectId, int X, int Y)[] placements)
    {
        var payload = new List<byte>();
        AddUInt16(payload, width);
        AddUInt16(payload, height);
        payload.Add(0x10);
        AddUInt16(payload, 0);
        payload.Add(state);
        payload.Add(0);
        payload.Add(paletteId);
        payload.Add((byte)placements.Length);
        foreach (var (objectId, x, y) in placements)
        {
            AddUInt16(payload, objectId);
            payload.Add(0);
            payload.Add(0);
            AddUInt16(payload, x);
            AddUInt16(payload, y);
        }

        return Segment(seconds, 0x16, [.. payload]);
    }

    public PgsStreamBuilder CroppedComposition(
        double seconds,
        int width,
        int height,
        byte paletteId,
        int objectId,
        int x,
        int y,
        int cropX,
        int cropY,
        int cropWidth,
        int cropHeight)
    {
        var payload = new List<byte>();
        AddUInt16(payload, width);
        AddUInt16(payload, height);
        payload.Add(0x10);
        AddUInt16(payload, 0);
        payload.Add(EpochStart);
        payload.Add(0);
        payload.Add(paletteId);
        payload.Add(1);
        AddUInt16(payload, objectId);
        payload.Add(0);
        payload.Add(0x80);
        AddUInt16(payload, x);
        AddUInt16(payload, y);
        AddUInt16(payload, cropX);
        AddUInt16(payload, cropY);
        AddUInt16(payload, cropWidth);
        AddUInt16(payload, cropHeight);
        return Segment(seconds, 0x16, [.. payload]);
    }

    /// <summary>Entries are (index, Y, Cr, Cb, alpha).</summary>
    public PgsStreamBuilder Palette(double seconds, byte id, params (byte Index, byte Y, byte Cr, byte Cb, byte Alpha)[] entries)
    {
        var payload = new List<byte> { id, 0 };
        foreach (var (index, y, cr, cb, alpha) in entries)
            payload.AddRange([index, y, cr, cb, alpha]);

        return Segment(seconds, 0x14, [.. payload]);
    }

    public PgsStreamBuilder Object(double seconds, int id, int width, int height, byte[] rle) =>
        ObjectFragments(seconds, id, width, height, rle, rle.Length);

    /// <summary>Splits the RLE data after <paramref name="firstFragmentLength"/> bytes.</summary>
    public PgsStreamBuilder ObjectFragments(
        double seconds,
        int id,
        int width,
        int height,
        byte[] rle,
        int firstFragmentLength)
    {
        var single = firstFragmentLength >= rle.Length;
        var first = new List<byte>();
        AddUInt16(first, id);
        first.Add(0);
        first.Add(single ? (byte)0xC0 : (byte)0x80);
        var dataLength = rle.Length + 4;
        first.AddRange([(byte)(dataLength >> 16), (byte)(dataLength >> 8), (byte)dataLength]);
        AddUInt16(first, width);
        AddUInt16(first, height);
        first.AddRange(rle.Take(firstFragmentLength));
        Segment(seconds, 0x15, [.. first]);
        if (single)
            return this;

        var last = new List<byte>();
        AddUInt16(last, id);
        last.Add(0);
        last.Add(0x40);
        last.AddRange(rle.Skip(firstFragmentLength));
        return Segment(seconds, 0x15, [.. last]);
    }

    public PgsStreamBuilder End(double seconds) => Segment(seconds, 0x80, []);

    /// <summary>A white 2x2 subtitle shown at <paramref name="seconds"/>.</summary>
    public PgsStreamBuilder Show(double seconds, int x = 100, int y = 900) =>
        Composition(seconds, 1920, 1080, EpochStart, 0, (1, x, y))
            .Palette(seconds, 0, (1, 235, 128, 128, 255), (2, 16, 128, 128, 128))
            .Object(seconds, 1, 2, 2, TwoByTwoRle)
            .End(seconds);

    public PgsStreamBuilder Clear(double seconds) =>
        Composition(seconds, 1920, 1080, NormalCase, 0).End(seconds);

    private PgsStreamBuilder Segment(double seconds, byte type, byte[] payload)
    {
        var pts = (uint)Math.Round(seconds * 90000);
        _stream.Write([(byte)'P', (byte)'G']);
        _stream.Write([(byte)(pts >> 24), (byte)(pts >> 16), (byte)(pts >> 8), (byte)pts]);
        _stream.Write([0, 0, 0, 0]);
        _stream.WriteByte(type);
        _stream.Write([(byte)(payload.Length >> 8), (byte)payload.Length]);
        _stream.Write(payload);
        return this;
    }

    private static void AddUInt16(List<byte> target, int value)
    {
        target.Add((byte)(value >> 8));
        target.Add((byte)value);
    }
}
