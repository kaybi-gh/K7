namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Incremental parser for a PGS (.sup) stream: feed it bytes as they arrive, get the
/// decoded screens back. Palettes and objects live through an epoch, so a display set can
/// place objects an earlier one defined. A display set whose object or palette was never
/// seen (window opened mid-epoch) is dropped, never treated as a clear.
/// </summary>
public sealed class PgsParser
{
    private const int HeaderSize = 13;
    private const byte SegmentPalette = 0x14;
    private const byte SegmentObject = 0x15;
    private const byte SegmentComposition = 0x16;
    private const byte SegmentEnd = 0x80;
    private const double ClockHz = 90000.0;

    private readonly Dictionary<int, byte[]> _palettes = [];
    private readonly Dictionary<int, PgsObject> _objects = [];
    private byte[] _buffer = new byte[64 * 1024];
    private int _length;
    private Composition? _composition;
    private bool _highDefinition = true;

    /// <summary>
    /// Appends stream bytes and returns the screens completed by them, in stream order.
    /// </summary>
    public IReadOnlyList<PgsFrame> Feed(ReadOnlySpan<byte> data)
    {
        if (data.Length == 0)
            return [];

        Append(data);

        List<PgsFrame>? frames = null;
        var offset = 0;
        while (_length - offset >= HeaderSize)
        {
            if (_buffer[offset] != (byte)'P' || _buffer[offset + 1] != (byte)'G')
            {
                // Lost sync (truncated window): walk to the next segment marker.
                offset++;
                continue;
            }

            var size = ReadUInt16(_buffer, offset + 11);
            if (_length - offset < HeaderSize + size)
                break;

            var pts = ReadUInt32(_buffer, offset + 2);
            var type = _buffer[offset + 10];
            var frame = ReadSegment(type, pts / ClockHz, _buffer.AsSpan(offset + HeaderSize, size));
            if (frame is not null)
                (frames ??= []).Add(frame);

            offset += HeaderSize + size;
        }

        if (offset > 0)
        {
            Buffer.BlockCopy(_buffer, offset, _buffer, 0, _length - offset);
            _length -= offset;
        }

        return frames ?? (IReadOnlyList<PgsFrame>)[];
    }

    private void Append(ReadOnlySpan<byte> data)
    {
        var needed = _length + data.Length;
        if (needed > _buffer.Length)
            Array.Resize(ref _buffer, Math.Max(needed, _buffer.Length * 2));

        data.CopyTo(_buffer.AsSpan(_length));
        _length = needed;
    }

    private PgsFrame? ReadSegment(byte type, double timeSeconds, ReadOnlySpan<byte> payload)
    {
        switch (type)
        {
            case SegmentComposition:
                ReadComposition(timeSeconds, payload);
                return null;
            case SegmentPalette:
                ReadPalette(payload);
                return null;
            case SegmentObject:
                ReadObject(payload);
                return null;
            case SegmentEnd:
                return Compose();
            default:
                // Window definitions only bound what the objects may cover.
                return null;
        }
    }

    private void ReadComposition(double timeSeconds, ReadOnlySpan<byte> payload)
    {
        _composition = null;
        if (payload.Length < 11)
            return;

        var width = ReadUInt16(payload, 0);
        var height = ReadUInt16(payload, 2);
        var state = payload[7];
        var paletteId = payload[9];
        var count = payload[10];

        // Epoch start: nothing defined before it may be referenced again.
        if ((state & 0x80) != 0)
        {
            _objects.Clear();
            _palettes.Clear();
        }

        _highDefinition = height > 576;

        var placements = new List<Placement>(count);
        var offset = 11;
        for (var i = 0; i < count; i++)
        {
            if (payload.Length - offset < 8)
                return;

            var objectId = ReadUInt16(payload, offset);
            var flags = payload[offset + 3];
            var x = ReadUInt16(payload, offset + 4);
            var y = ReadUInt16(payload, offset + 6);
            offset += 8;

            Crop? crop = null;
            if ((flags & 0x80) != 0)
            {
                if (payload.Length - offset < 8)
                    return;

                crop = new Crop(
                    ReadUInt16(payload, offset),
                    ReadUInt16(payload, offset + 2),
                    ReadUInt16(payload, offset + 4),
                    ReadUInt16(payload, offset + 6));
                offset += 8;
            }

            placements.Add(new Placement(objectId, x, y, crop));
        }

        _composition = new Composition(timeSeconds, width, height, paletteId, placements);
    }

    private void ReadPalette(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 2)
            return;

        var id = payload[0];
        if (!_palettes.TryGetValue(id, out var palette))
        {
            // Entries a palette never defines stay fully transparent.
            palette = new byte[256 * 4];
            _palettes[id] = palette;
        }

        for (var offset = 2; offset + 5 <= payload.Length; offset += 5)
        {
            var entry = payload[offset] * 4;
            WriteColor(
                palette.AsSpan(entry, 4),
                payload[offset + 1],
                payload[offset + 2],
                payload[offset + 3],
                payload[offset + 4],
                _highDefinition);
        }
    }

    private void ReadObject(ReadOnlySpan<byte> payload)
    {
        if (payload.Length < 4)
            return;

        var id = ReadUInt16(payload, 0);
        var sequence = payload[3];
        if ((sequence & 0x80) != 0)
        {
            if (payload.Length < 11)
                return;

            // The declared length counts the 4 width/height bytes that follow it.
            var dataLength = ((payload[4] << 16) | (payload[5] << 8) | payload[6]) - 4;
            var width = ReadUInt16(payload, 7);
            var height = ReadUInt16(payload, 9);
            if (dataLength < 0 || width == 0 || height == 0)
            {
                _objects.Remove(id);
                return;
            }

            var pgsObject = new PgsObject(width, height, new byte[dataLength]);
            pgsObject.Append(payload[11..]);
            _objects[id] = pgsObject;
            return;
        }

        if (_objects.TryGetValue(id, out var existing))
            existing.Append(payload[4..]);
    }

    private PgsFrame? Compose()
    {
        var composition = _composition;
        _composition = null;
        if (composition is null)
            return null;

        if (composition.Placements.Count == 0)
            return new PgsFrame(composition.TimeSeconds, composition.Width, composition.Height, []);

        if (!_palettes.TryGetValue(composition.PaletteId, out var palette))
            return null;

        var objects = new List<PgsFrameObject>(composition.Placements.Count);
        foreach (var placement in composition.Placements)
        {
            if (!_objects.TryGetValue(placement.ObjectId, out var pgsObject) || !pgsObject.IsComplete)
                return null;

            var rgba = Decode(pgsObject, palette);
            var width = pgsObject.Width;
            var height = pgsObject.Height;
            if (placement.Crop is { } crop)
            {
                var cropped = ApplyCrop(rgba, width, height, crop, out var croppedWidth, out var croppedHeight);
                if (cropped is null)
                    continue;

                rgba = cropped;
                width = croppedWidth;
                height = croppedHeight;
            }

            objects.Add(new PgsFrameObject(placement.X, placement.Y, width, height, rgba));
        }

        return new PgsFrame(composition.TimeSeconds, composition.Width, composition.Height, objects);
    }

    /// <summary>
    /// Run-length decode: a non-zero byte is one pixel; 0 opens a run whose flags give the
    /// length size (6 or 14 bits) and whether a colour follows (else colour 0); 0 0 ends a line.
    /// </summary>
    private static byte[] Decode(PgsObject pgsObject, byte[] palette)
    {
        var width = pgsObject.Width;
        var height = pgsObject.Height;
        var rgba = new byte[width * height * 4];
        var data = pgsObject.Data;
        var end = pgsObject.Length;
        var position = 0;
        var x = 0;
        var y = 0;

        while (position < end && y < height)
        {
            var color = data[position++];
            var run = 1;
            if (color == 0)
            {
                if (position >= end)
                    break;

                var flags = data[position++];
                run = flags & 0x3F;
                if ((flags & 0x40) != 0)
                {
                    if (position >= end)
                        break;

                    run = (run << 8) | data[position++];
                }

                if ((flags & 0x80) != 0)
                {
                    if (position >= end)
                        break;

                    color = data[position++];
                }

                if (run == 0)
                {
                    x = 0;
                    y++;
                    continue;
                }
            }

            var count = Math.Min(run, width - x);
            if (count > 0 && palette[(color * 4) + 3] != 0)
            {
                var source = palette.AsSpan(color * 4, 4);
                var target = rgba.AsSpan(((y * width) + x) * 4, count * 4);
                for (var i = 0; i < target.Length; i += 4)
                    source.CopyTo(target.Slice(i, 4));
            }

            x += run;
        }

        return rgba;
    }

    private static byte[]? ApplyCrop(
        byte[] rgba,
        int width,
        int height,
        Crop crop,
        out int croppedWidth,
        out int croppedHeight)
    {
        croppedWidth = Math.Min(crop.Width, width - crop.X);
        croppedHeight = Math.Min(crop.Height, height - crop.Y);
        if (croppedWidth <= 0 || croppedHeight <= 0)
            return null;

        var cropped = new byte[croppedWidth * croppedHeight * 4];
        for (var row = 0; row < croppedHeight; row++)
        {
            Buffer.BlockCopy(
                rgba,
                (((crop.Y + row) * width) + crop.X) * 4,
                cropped,
                row * croppedWidth * 4,
                croppedWidth * 4);
        }

        return cropped;
    }

    /// <summary>
    /// Limited-range YCbCr to RGB: BT.709 for HD planes, BT.601 for SD.
    /// </summary>
    private static void WriteColor(Span<byte> target, byte luma, byte cr, byte cb, byte alpha, bool highDefinition)
    {
        var y = (luma - 16) * 1.164;
        var u = cb - 128.0;
        var v = cr - 128.0;

        double red;
        double green;
        double blue;
        if (highDefinition)
        {
            red = y + (1.793 * v);
            green = y - (0.213 * u) - (0.533 * v);
            blue = y + (2.112 * u);
        }
        else
        {
            red = y + (1.596 * v);
            green = y - (0.392 * u) - (0.813 * v);
            blue = y + (2.017 * u);
        }

        target[0] = ToByte(red);
        target[1] = ToByte(green);
        target[2] = ToByte(blue);
        target[3] = alpha;
    }

    private static byte ToByte(double value) => (byte)Math.Clamp((int)Math.Round(value), 0, 255);

    private static int ReadUInt16(ReadOnlySpan<byte> data, int offset) =>
        (data[offset] << 8) | data[offset + 1];

    private static long ReadUInt32(ReadOnlySpan<byte> data, int offset) =>
        ((long)data[offset] << 24) | ((long)data[offset + 1] << 16) | ((long)data[offset + 2] << 8) | data[offset + 3];

    private sealed record Composition(
        double TimeSeconds,
        int Width,
        int Height,
        int PaletteId,
        IReadOnlyList<Placement> Placements);

    private sealed record Placement(int ObjectId, int X, int Y, Crop? Crop);

    private readonly record struct Crop(int X, int Y, int Width, int Height);

    private sealed class PgsObject(int width, int height, byte[] data)
    {
        public int Width { get; } = width;

        public int Height { get; } = height;

        public byte[] Data { get; } = data;

        public int Length { get; private set; }

        public bool IsComplete => Length == Data.Length;

        public void Append(ReadOnlySpan<byte> fragment)
        {
            var count = Math.Min(fragment.Length, Data.Length - Length);
            if (count <= 0)
                return;

            fragment[..count].CopyTo(Data.AsSpan(Length));
            Length += count;
        }
    }
}
