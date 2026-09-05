using System.Buffers.Binary;
using AwesomeAssertions;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class Fmp4TfdtRebaseTests
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "k7-tfdt-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldShiftTfdt_WhenWindowResetToZero()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 0),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        // Segment starts at 6000ms -> expected base = 144000
        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out var rebased,
            out var detail);

        ok.Should().BeTrue(detail);
        detail.Should().Contain("rebased");
        detail.Should().Contain("to~=144000");
        // Verify the first tfdt baseMediaDecodeTime bytes directly (v1: after version/flags).
        var baseOffset = IndexOfTfdtBase(rebased);
        baseOffset.Should().BeGreaterThan(0);
        BinaryPrimitives.ReadUInt64BigEndian(rebased.AsSpan(baseOffset, 8)).Should().Be(144000);
    }

    [Test]
    public void IsSafeToFinalize_ShouldWaitForSuccessor_WhenFfmpegStillRunning()
    {
        Fmp4TfdtRebase.IsSafeToFinalize(_tempDirectory, 3, ffmpegExited: false).Should().BeFalse();
        File.WriteAllBytes(Path.Combine(_tempDirectory, "4.m4s"), [1, 2, 3]);
        Fmp4TfdtRebase.IsSafeToFinalize(_tempDirectory, 3, ffmpegExited: false).Should().BeTrue();
        Fmp4TfdtRebase.IsSafeToFinalize(_tempDirectory, 9, ffmpegExited: true).Should().BeTrue();
    }

    [Test]
    public void TryFinalizeClosedSegment_ShouldPersistShiftedTfdt_WhenWindowRelative()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 0),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));
        var path = Path.Combine(_tempDirectory, "1.m4s");
        File.WriteAllBytes(path, segment);

        var ok = Fmp4TfdtRebase.TryFinalizeClosedSegment(
            path,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            Hls.TfdtWindowResetThresholdMs,
            alignPresentationTime: false,
            out var detail);

        ok.Should().BeTrue(detail);
        var onDisk = File.ReadAllBytes(path);
        var baseOffset = IndexOfTfdtBase(onDisk);
        BinaryPrimitives.ReadUInt64BigEndian(onDisk.AsSpan(baseOffset, 8)).Should().Be(144000);
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldPreserveRelativeGaps_WhenPatchingMultipleMoof()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 1000),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]),
            BuildMinimalMoof(tfdtBaseDecodeTime: 5000),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out var rebased,
            out var detail);

        ok.Should().BeTrue(detail);
        detail.Should().Contain("boxes=2");
        var firstBase = IndexOfTfdtBase(rebased);
        BinaryPrimitives.ReadUInt64BigEndian(rebased.AsSpan(firstBase, 8)).Should().Be(144000);
        // 148000 = 0x24220
        Convert.ToHexString(rebased).Should().Contain("0000000000024220");

        // Segment must remain a valid fMP4 for the waiter.
        HlsSegmentFileWaiter.IsValidFmp4Segment(rebased, isInit: false).Should().BeTrue(detail);
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldSkip_WhenAlreadyAbsolute()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 144000),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out _,
            out var detail);

        ok.Should().BeFalse();
        detail.Should().Contain("already-absolute");
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldSkip_WhenDriftIsCompositionError()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        // 83ms at 24kHz = 1992 units. Independent rebase of this vs audio PTS
        // would create a constant lip-sync offset on Web and ExoPlayer.
        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 145992),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out _,
            out var detail);

        ok.Should().BeFalse();
        detail.Should().Contain("already-absolute");
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldShiftTfdt_WhenVideoToleranceIsTwentyMs()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 145992),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out var rebased,
            out var detail,
            toleranceMs: Hls.VideoTfdtAlignToleranceMs);

        ok.Should().BeTrue(detail);
        detail.Should().Contain("rebased");
        var baseOffset = IndexOfTfdtBase(rebased);
        BinaryPrimitives.ReadUInt64BigEndian(rebased.AsSpan(baseOffset, 8)).Should().Be(144000);
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldSkipEncoderDelay_WhenCopyToleranceIsOneSecond()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        // 400ms hardware encode delay at 24kHz = 9600 units. Under the remux 1s
        // window, so copy must not flatten it. Encode uses 20ms and must.
        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 153600),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var skipped = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out _,
            out var skipDetail);

        skipped.Should().BeFalse();
        skipDetail.Should().Contain("already-absolute");

        var aligned = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out var rebased,
            out var alignDetail,
            toleranceMs: Hls.VideoTfdtAlignToleranceMs);

        aligned.Should().BeTrue(alignDetail);
        var baseOffset = IndexOfTfdtBase(rebased);
        BinaryPrimitives.ReadUInt64BigEndian(rebased.AsSpan(baseOffset, 8)).Should().Be(144000);
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldShiftTfdt_WhenWindowResetExceedsOneSecond()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        // 1500ms behind playlist at 24kHz = 36000 units.
        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 108000),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out var rebased,
            out var detail);

        ok.Should().BeTrue(detail);
        detail.Should().Contain("rebased");
        var baseOffset = IndexOfTfdtBase(rebased);
        BinaryPrimitives.ReadUInt64BigEndian(rebased.AsSpan(baseOffset, 8)).Should().Be(144000);
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldSkipCompositionFlatten_WhenEncodeAlignRequested()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 145992),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out _,
            out var detail,
            toleranceMs: Hls.VideoTfdtAlignToleranceMs,
            alignPresentationTime: true);

        ok.Should().BeFalse();
        detail.Should().Contain("already-absolute");
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldSubtractFirstSampleCts_WhenAligningPresentation()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        // Playlist 6000ms = 144000. Encoder CTS +14160 (~590ms) must be removed
        // from tfdt so the first frame presents at the playlist start.
        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 0, compositionOffset: 14160),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out var rebased,
            out var detail,
            toleranceMs: Hls.VideoTfdtAlignToleranceMs,
            alignPresentationTime: true);

        ok.Should().BeTrue(detail);
        detail.Should().Contain("cts=14160");
        var baseOffset = IndexOfTfdtBase(rebased);
        BinaryPrimitives.ReadUInt64BigEndian(rebased.AsSpan(baseOffset, 8)).Should().Be(144000 - 14160);
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldShiftAlreadyAbsoluteTfdt_WhenEncodeCtsRemains()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 144000, compositionOffset: 14160),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var skipped = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out _,
            out var skipDetail,
            toleranceMs: Hls.VideoTfdtAlignToleranceMs);

        skipped.Should().BeFalse();
        skipDetail.Should().Contain("already-absolute");

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out var rebased,
            out var detail,
            toleranceMs: Hls.VideoTfdtAlignToleranceMs,
            alignPresentationTime: true);

        ok.Should().BeTrue(detail);
        var baseOffset = IndexOfTfdtBase(rebased);
        BinaryPrimitives.ReadUInt64BigEndian(rebased.AsSpan(baseOffset, 8)).Should().Be(144000 - 14160);
    }

    [Test]
    public void TryRebaseMediaSegment_ShouldPullDown_WhenStaleBaseTooHigh()
    {
        const uint timescale = 24000;
        WriteInit(timescale);

        // Previously rebased onto a wrong equal-length grid (~10s instead of 6s).
        var segment = Concat(
            BuildMinimalMoof(tfdtBaseDecodeTime: 240000),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));

        var ok = Fmp4TfdtRebase.TryRebaseMediaSegment(
            segment,
            Path.Combine(_tempDirectory, "init.m4s"),
            segmentStartTimestampMs: 6000,
            out var rebased,
            out var detail);

        ok.Should().BeTrue(detail);
        detail.Should().Contain("rebased tfdt -");
        var baseOffset = IndexOfTfdtBase(rebased);
        BinaryPrimitives.ReadUInt64BigEndian(rebased.AsSpan(baseOffset, 8)).Should().Be(144000);
    }

    private static int IndexOfTfdtBase(byte[] data)
    {
        // Find "tfdt" box and return offset of 64-bit base (payload+4).
        for (var i = 0; i + 16 <= data.Length; i++)
        {
            if (data[i] == (byte)'t'
                && data[i + 1] == (byte)'f'
                && data[i + 2] == (byte)'d'
                && data[i + 3] == (byte)'t')
            {
                return i + 8; // type ends at i+4, version/flags 4 bytes, then base
            }
        }

        return -1;
    }

    private void WriteInit(uint timescale)
    {
        // mdhd v0: ver/flags(4) + creation(4) + modification(4) + timescale(4) + duration(4) + lang(2) + pre(2)
        var mdhdPayload = new byte[24];
        BinaryPrimitives.WriteUInt32BigEndian(mdhdPayload.AsSpan(12, 4), timescale);

        var mdhd = BuildBox("mdhd", mdhdPayload);
        var mdia = BuildBox("mdia", mdhd);
        var trak = BuildBox("trak", mdia);
        var moov = BuildBox("moov", trak);
        var init = Concat(BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]), moov);
        File.WriteAllBytes(Path.Combine(_tempDirectory, "init.m4s"), init);
    }

    private static byte[] BuildBox(string type, byte[] payload)
    {
        var size = 8 + payload.Length;
        var box = new byte[size];
        box[0] = (byte)(size >> 24);
        box[1] = (byte)(size >> 16);
        box[2] = (byte)(size >> 8);
        box[3] = (byte)size;
        box[4] = (byte)type[0];
        box[5] = (byte)type[1];
        box[6] = (byte)type[2];
        box[7] = (byte)type[3];
        Buffer.BlockCopy(payload, 0, box, 8, payload.Length);
        return box;
    }

    private static byte[] BuildMinimalMoof(long tfdtBaseDecodeTime, int? compositionOffset = null)
    {
        using var tfhdPayload = new MemoryStream();
        WriteUInt32(tfhdPayload, 0x020000u);
        WriteUInt32(tfhdPayload, 1);

        using var tfdtPayload = new MemoryStream();
        WriteUInt32(tfdtPayload, 0x0100_0000u);
        WriteUInt64(tfdtPayload, unchecked((ulong)tfdtBaseDecodeTime));

        using var trunPayload = new MemoryStream();
        var trunFlags = compositionOffset is null
            ? 0x00000201u
            : 0x01000A01u; // v1 + data_offset | sample_size | composition
        WriteUInt32(trunPayload, trunFlags);
        WriteUInt32(trunPayload, 1);
        WriteInt32(trunPayload, 8);
        WriteUInt32(trunPayload, 8);
        if (compositionOffset is int cts)
            WriteInt32(trunPayload, cts);

        var mfhd = BuildBox("mfhd", [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01]);
        var traf = BuildBox(
            "traf",
            Concat(
                BuildBox("tfhd", tfhdPayload.ToArray()),
                BuildBox("tfdt", tfdtPayload.ToArray()),
                BuildBox("trun", trunPayload.ToArray())));
        return BuildBox("moof", Concat(mfhd, traf));
    }

    private static void WriteUInt32(Stream stream, uint value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static void WriteInt32(Stream stream, int value) =>
        WriteUInt32(stream, unchecked((uint)value));

    private static void WriteUInt64(Stream stream, ulong value)
    {
        WriteUInt32(stream, (uint)(value >> 32));
        WriteUInt32(stream, (uint)value);
    }

    private static byte[] Concat(params byte[][] parts)
    {
        var length = parts.Sum(p => p.Length);
        var result = new byte[length];
        var offset = 0;
        foreach (var part in parts)
        {
            Buffer.BlockCopy(part, 0, result, offset, part.Length);
            offset += part.Length;
        }

        return result;
    }
}
