using K7.Server.Application.Helpers;
using K7.Server.Domain.Interfaces;

namespace K7.Server.Application.UnitTests.Helpers;

[TestFixture]
public class HlsSegmentFileWaiterTests
{
    private string _tempDirectory = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "k7-hls-waiter-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public void HasNonEmptyContent_ShouldReturnFalse_WhenFileMissing()
    {
        var path = Path.Combine(_tempDirectory, "init.m4s");

        HlsSegmentFileWaiter.HasNonEmptyContent(path).Should().BeFalse();
    }

    [Test]
    public void HasNonEmptyContent_ShouldReturnFalse_WhenFileIsEmpty()
    {
        var path = Path.Combine(_tempDirectory, "init.m4s");
        File.WriteAllBytes(path, []);

        HlsSegmentFileWaiter.HasNonEmptyContent(path).Should().BeFalse();
    }

    [Test]
    public void HasNonEmptyContent_ShouldReturnTrue_WhenFileHasBytes()
    {
        var path = Path.Combine(_tempDirectory, "init.m4s");
        File.WriteAllBytes(path, [0x00, 0x00, 0x00, 0x18]);

        HlsSegmentFileWaiter.HasNonEmptyContent(path).Should().BeTrue();
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnFalse_WhenInitIsNonEmptyButMissingMoov()
    {
        var path = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        File.WriteAllBytes(path, BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]));

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse();
        HlsSegmentFileWaiter.TryReadReadySegmentBytes(path, out _).Should().BeFalse();
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnFalse_WhenInitIsTruncatedMidBox()
    {
        var path = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        // Declares a 1024-byte box but only provides a few payload bytes.
        File.WriteAllBytes(path, [0x00, 0x00, 0x04, 0x00, 0x66, 0x74, 0x79, 0x70, 0x69, 0x73, 0x6F, 0x6D]);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse();
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnFalse_WhenSizeHasTopBitSet()
    {
        var path = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        // 0xFFFFFC00 == -1024 as signed int - ExoPlayer "Top bit not zero: -1024"
        File.WriteAllBytes(path, [0xFF, 0xFF, 0xFC, 0x00, 0x66, 0x74, 0x79, 0x70]);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse();
        HlsSegmentFileWaiter.IsValidFmp4Segment(
                [0xFF, 0xFF, 0xFC, 0x00, 0x66, 0x74, 0x79, 0x70],
                isInit: true)
            .Should().BeFalse();
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnTrue_WhenInitHasFtypAndMoov()
    {
        var path = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        var bytes = Concat(BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]), BuildBox("moov", [0x00]));
        File.WriteAllBytes(path, bytes);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeTrue();
        HlsSegmentFileWaiter.TryReadReadySegmentBytes(path, out var read).Should().BeTrue();
        read.Should().Equal(bytes);
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnTrue_WhenMediaHasMoofAndMdat()
    {
        var path = Path.Combine(_tempDirectory, "0.m4s");
        var bytes = Concat(BuildMinimalMoof(sampleSizes: [10, 20]), BuildBox("mdat", [0x02, 0x03, 0x04, 0x05, 0x06, 0x07, 0x08, 0x09, 0x0A, 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14, 0x15, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x1B, 0x1C, 0x1D, 0x1E]));
        File.WriteAllBytes(path, bytes);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeTrue();
        HlsSegmentFileWaiter.DescribeMoofTrun(bytes).Should().Contain("trun").And.Contain("count=2");
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnFalse_WhenTrunHasNegativeCts()
    {
        var path = Path.Combine(_tempDirectory, "0.m4s");
        // -1024 as signed int32 = 0xFFFFFC00 - the ExoPlayer failure value
        var bytes = Concat(
            BuildMinimalMoof(sampleSizes: [8], compositionOffsets: [-1024], trunVersion: 1),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));
        File.WriteAllBytes(path, bytes);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse();
        HlsSegmentFileWaiter.DescribeUnreadiness(path).Should().Contain("trun-cts-negative");
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnFalse_WhenTfdtHasNegativeBaseDecodeTime()
    {
        var path = Path.Combine(_tempDirectory, "0.m4s");
        // AAC priming with avoid_negative_ts disabled writes tfdt = -1024
        // (0xFFFFFFFFFFFFFC00) -> ExoPlayer "Top bit not zero: -1024".
        var bytes = Concat(
            BuildMinimalMoof(sampleSizes: [8], tfdtBaseDecodeTime: -1024),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));
        File.WriteAllBytes(path, bytes);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse();
        HlsSegmentFileWaiter.DescribeUnreadiness(path).Should().Contain("tfdt-negative-or-top-bit");
        HlsSegmentFileWaiter.DescribeMoofTrun(bytes).Should().Contain("signedLow=-1024");
    }

    [Test]
    public void DescribeMoofTrun_ShouldIncludeTfdtBase_WhenValid()
    {
        var bytes = Concat(
            BuildMinimalMoof(sampleSizes: [8, 16], tfdtBaseDecodeTime: 48000),
            BuildBox("mdat", new byte[24]));

        HlsSegmentFileWaiter.DescribeMoofTrun(bytes).Should().Contain("tfdt v=1 base=48000");
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnFalse_WhenTfhdHasBaseDataOffset()
    {
        var path = Path.Combine(_tempDirectory, "0.m4s");
        var bytes = Concat(
            BuildMinimalMoof(sampleSizes: [8], includeBaseDataOffset: true),
            BuildBox("mdat", [1, 2, 3, 4, 5, 6, 7, 8]));
        File.WriteAllBytes(path, bytes);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse();
        HlsSegmentFileWaiter.DescribeUnreadiness(path).Should().Contain("tfhd-has-base-data-offset");
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnFalse_WhenMediaMissingMdat()
    {
        var path = Path.Combine(_tempDirectory, "0.m4s");
        File.WriteAllBytes(path, BuildMinimalMoof(sampleSizes: [8]));

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse();
    }

    [Test]
    public async Task WaitUntilAvailableAsync_ShouldWaitUntilInitIsValidFmp4()
    {
        var initPath = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        var job = CreateJob();
        var generationStarted = 0;

        var waitTask = HlsSegmentFileWaiter.WaitUntilAvailableAsync(
            initPath,
            job,
            _ =>
            {
                generationStarted++;
                return Task.CompletedTask;
            },
            CancellationToken.None,
            maxTotalSeconds: 5,
            pollingIntervalMs: 50);

        await Task.Delay(100);
        // Non-empty but incomplete - must keep waiting.
        File.WriteAllBytes(initPath, BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]));
        await Task.Delay(150);
        File.WriteAllBytes(
            initPath,
            Concat(BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]), BuildBox("moov", [0x00])));

        var result = await waitTask;

        result.Should().BeNull();
        generationStarted.Should().BeGreaterThanOrEqualTo(1);
        HlsSegmentFileWaiter.IsSegmentFileReady(initPath).Should().BeTrue();
    }

    [Test]
    public async Task WaitUntilAvailableAsync_ShouldTimeout_WhenInitStaysEmpty()
    {
        var initPath = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        File.WriteAllBytes(initPath, []);
        var job = CreateJob();

        var result = await HlsSegmentFileWaiter.WaitUntilAvailableAsync(
            initPath,
            job,
            _ => Task.CompletedTask,
            CancellationToken.None,
            maxTotalSeconds: 1,
            pollingIntervalMs: 50);

        result.Should().BeOfType<TimeoutException>();
    }

    [Test]
    public async Task WaitUntilAvailableAsync_ShouldTimeout_WhenInitStaysTruncated()
    {
        var initPath = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);
        File.WriteAllBytes(initPath, BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]));
        var job = CreateJob();

        var result = await HlsSegmentFileWaiter.WaitUntilAvailableAsync(
            initPath,
            job,
            _ => Task.CompletedTask,
            CancellationToken.None,
            maxTotalSeconds: 1,
            pollingIntervalMs: 50);

        result.Should().BeOfType<TimeoutException>();
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnFalse_WhenMediaHasSizeZeroMdatUntilStable()
    {
        var path = Path.Combine(_tempDirectory, "0.m4s");
        // moof (explicit) + mdat with size 0 spanning the rest - looks "complete" to a naive walk
        // but must not be served until length is stable (simulates ffmpeg still appending).
        var moof = BuildMinimalMoof(sampleSizes: [1]);
        var mdatHeader = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x6D, 0x64, 0x61, 0x74, 0xAA };
        var bytes = Concat(moof, mdatHeader);
        File.WriteAllBytes(path, bytes);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse(
            "size-zero mdat must not be ready on the first observation");
        HlsSegmentFileWaiter.DescribeUnreadiness(path).Should().Contain("size-zero");
    }

    [Test]
    public async Task IsSegmentFileReady_ShouldReturnTrue_WhenSizeZeroMdatLengthIsStable()
    {
        var path = Path.Combine(_tempDirectory, "0.m4s");
        var moof = BuildMinimalMoof(sampleSizes: [2]);
        var mdatHeader = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x6D, 0x64, 0x61, 0x74, 0xAA, 0xBB };
        File.WriteAllBytes(path, Concat(moof, mdatHeader));

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeFalse();

        await Task.Delay(HlsSegmentFileWaiter.SizeZeroStableMs + 50);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeTrue();
        HlsSegmentFileWaiter.TryReadReadySegmentBytes(path, out var read).Should().BeTrue();
        read.Length.Should().Be(moof.Length + mdatHeader.Length);
    }

    [Test]
    public void IsSegmentFileReady_ShouldReturnTrue_WhenSizeZeroMdatAndNextSegmentExists()
    {
        var path = Path.Combine(_tempDirectory, "0.m4s");
        var moof = BuildMinimalMoof(sampleSizes: [1]);
        var mdatHeader = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x6D, 0x64, 0x61, 0x74, 0xAA };
        File.WriteAllBytes(path, Concat(moof, mdatHeader));
        File.WriteAllBytes(Path.Combine(_tempDirectory, "1.m4s"), [0x01]);

        HlsSegmentFileWaiter.IsSegmentFileReady(path).Should().BeTrue(
            "next segment implies ffmpeg closed the previous size-zero mdat");
    }

    [Test]
    public void FormatLeadingBytesHex_ShouldFormatBytes()
    {
        HlsSegmentFileWaiter.FormatLeadingBytesHex([0x00, 0x00, 0x00, 0x18, 0x66, 0x74, 0x79, 0x70], 8)
            .Should().Be("00 00 00 18 66 74 79 70");
    }

    [Test]
    public void DescribeTopLevelBoxes_ShouldListFtypAndMoov()
    {
        var bytes = Concat(BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]), BuildBox("moov", [0x00]));
        HlsSegmentFileWaiter.DescribeTopLevelBoxes(bytes).Should().Contain("ftyp:").And.Contain("moov:");
    }

    [Test]
    public void GetInitSegmentPath_ShouldCombineOutputDirectoryAndInitFileName()
    {
        var path = HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory);

        path.Should().Be(Path.Combine(_tempDirectory, HlsSegmentFileWaiter.InitSegmentFileName));
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenSegmentMissing()
    {
        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeFalse();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenSegment0ExistsButInitIsEmpty()
    {
        File.WriteAllBytes(
            Path.Combine(_tempDirectory, "0.m4s"),
            Concat(BuildMinimalMoof(sampleSizes: [1]), BuildBox("mdat", [0x02])));
        File.WriteAllBytes(HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory), []);

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeFalse();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenSegment0ExistsButInitMissing()
    {
        File.WriteAllBytes(
            Path.Combine(_tempDirectory, "0.m4s"),
            Concat(BuildMinimalMoof(sampleSizes: [1]), BuildBox("mdat", [0x02])));

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeFalse();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenSegment0ExistsButInitIsOnlyFtyp()
    {
        File.WriteAllBytes(
            Path.Combine(_tempDirectory, "0.m4s"),
            Concat(BuildMinimalMoof(sampleSizes: [1]), BuildBox("mdat", [0x02])));
        File.WriteAllBytes(
            HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory),
            BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]));

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeFalse();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnTrue_WhenSegment0AndInitAreValid()
    {
        File.WriteAllBytes(
            Path.Combine(_tempDirectory, "0.m4s"),
            Concat(BuildMinimalMoof(sampleSizes: [1]), BuildBox("mdat", [0x02])));
        File.WriteAllBytes(
            HlsSegmentFileWaiter.GetInitSegmentPath(_tempDirectory),
            Concat(BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]), BuildBox("moov", [0x00])));

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 0).Should().BeTrue();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnTrue_WhenLaterSegmentIsValidWithoutInit()
    {
        File.WriteAllBytes(
            Path.Combine(_tempDirectory, "3.m4s"),
            Concat(BuildMinimalMoof(sampleSizes: [1]), BuildBox("mdat", [0x02])));

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 3).Should().BeTrue();
    }

    [Test]
    public void IsSegmentReadyOnDisk_ShouldReturnFalse_WhenLaterSegmentIsEmpty()
    {
        File.WriteAllBytes(Path.Combine(_tempDirectory, "2.m4s"), []);

        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(_tempDirectory, 2).Should().BeFalse();
    }

    private TranscodeJob CreateJob() =>
        new()
        {
            JobId = Guid.NewGuid(),
            IndexedFileId = Guid.NewGuid(),
            Quality = "original",
            VideoCodec = null,
            AudioCodec = "aac",
            AudioTrackIndex = 1,
            IsAudioOnly = true,
            OutputDirectory = _tempDirectory,
            InputFilePath = "input.mkv",
        };

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

    /// <summary>
    /// Minimal CMAF-like moof/traf/tfhd/tfdt/trun for waiter tests.
    /// </summary>
    private static byte[] BuildMinimalMoof(
        uint[] sampleSizes,
        int[]? compositionOffsets = null,
        byte trunVersion = 0,
        bool includeBaseDataOffset = false,
        long tfdtBaseDecodeTime = 0)
    {
        // tfhd: version/flags + track_id (+ optional base_data_offset)
        // flags: default_base_is_moof (0x20000) and optionally base_data_offset_present (0x1)
        var tfhdFlags = 0x020000u;
        if (includeBaseDataOffset)
            tfhdFlags |= 0x1;

        using var tfhdPayload = new MemoryStream();
        WriteUInt32(tfhdPayload, tfhdFlags); // version=0 in high byte
        WriteUInt32(tfhdPayload, 1); // track_id
        if (includeBaseDataOffset)
            WriteUInt64(tfhdPayload, 1234);

        var tfhd = BuildBox("tfhd", tfhdPayload.ToArray());

        // tfdt v1 with 64-bit baseMediaDecodeTime
        using var tfdtPayload = new MemoryStream();
        WriteUInt32(tfdtPayload, 0x0100_0000u); // version=1
        WriteUInt64(tfdtPayload, unchecked((ulong)tfdtBaseDecodeTime));
        var tfdt = BuildBox("tfdt", tfdtPayload.ToArray());

        // trun flags: data_offset_present | sample_size_present | optional cts
        var trunFlags = 0x000001u | 0x000200u;
        if (compositionOffsets is not null)
            trunFlags |= 0x000800u;

        using var trunPayload = new MemoryStream();
        WriteUInt32(trunPayload, ((uint)trunVersion << 24) | trunFlags);
        WriteUInt32(trunPayload, (uint)sampleSizes.Length);
        WriteInt32(trunPayload, 8); // data_offset placeholder
        for (var i = 0; i < sampleSizes.Length; i++)
        {
            WriteUInt32(trunPayload, sampleSizes[i]);
            if (compositionOffsets is not null)
                WriteInt32(trunPayload, compositionOffsets[i]);
        }

        var trun = BuildBox("trun", trunPayload.ToArray());
        var mfhd = BuildBox("mfhd", [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01]);
        var traf = BuildBox("traf", Concat(tfhd, tfdt, trun));
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
