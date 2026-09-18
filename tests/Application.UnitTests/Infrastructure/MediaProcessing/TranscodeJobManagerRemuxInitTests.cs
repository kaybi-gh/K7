using K7.Server.Application.Common.Configuration;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.MediaProcessing;
using K7.Shared.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class TranscodeJobManagerRemuxInitTests
{
    private string _tempDirectory = null!;
    private IMediaTranscoder _transcoder = null!;
    private TranscodeJobManager _manager = null!;

    [SetUp]
    public void SetUp()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "k7-transcode-init-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);

        _transcoder = Substitute.For<IMediaTranscoder>();
        _transcoder.StartVideoStreamingTranscodeAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<List<HlsSegment>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int?>())
            .Returns(call => Task.Delay(Timeout.Infinite, call.Arg<CancellationToken>()));

        var settingsProvider = Substitute.For<ITranscodeSettingsProvider>();
        settingsProvider.GetSettingsAsync(Arg.Any<CancellationToken>())
            .Returns(new TranscodeSettingsDto { TranscodeTempQuotaMb = 0, EncoderThrottleBufferSegments = 10 });

        _manager = new TranscodeJobManager(
            Substitute.For<ILogger<TranscodeJobManager>>(),
            _transcoder,
            Options.Create(new PathsConfiguration { Transcoding = _tempDirectory }),
            settingsProvider,
            Substitute.For<IServiceScopeFactory>());
    }

    [TearDown]
    public async Task TearDown()
    {
        await _manager.CleanupStaleJobsAsync(TimeSpan.Zero);
        if (Directory.Exists(_tempDirectory))
            Directory.Delete(_tempDirectory, recursive: true);
    }

    [Test]
    public async Task EnsureInit_ShouldKeepRemuxHead_WhenCachedWindowHasNoInit()
    {
        var job = await StartRemuxJobAsync();
        WriteReadyMedia(job.OutputDirectory, 1403);
        WriteReadyMedia(job.OutputDirectory, 1404);

        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, -1, CreateSegments(1409));
        job.RemuxHeads.Should().ContainSingle();
        var headId = job.RemuxHeads.Keys.Single();

        await Task.Delay(250);

        job.IsFfmpegRunning.Should().BeTrue();
        job.RemuxHeads.ContainsKey(headId).Should().BeTrue();
        HlsSegmentFileWaiter.IsInitReadyOnDisk(job.OutputDirectory).Should().BeFalse();

        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, -1, CreateSegments(1409));
        job.RemuxHeads.Should().ContainSingle();
        job.RemuxHeads.ContainsKey(headId).Should().BeTrue();
    }

    [Test]
    public async Task EnsureInit_ShouldPromoteInit_WhenFromZeroHeadDeletesStagingLanding()
    {
        _transcoder.StartVideoStreamingTranscodeAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<List<HlsSegment>>(),
                Arg.Any<int>(),
                Arg.Any<int>(),
                Arg.Any<CancellationToken>(),
                Arg.Any<string?>(),
                Arg.Any<string?>(),
                Arg.Any<int?>())
            .Returns(call =>
            {
                var staging = call.ArgAt<string>(1);
                Directory.CreateDirectory(staging);
                File.WriteAllBytes(
                    Path.Combine(staging, HlsSegmentFileWaiter.InitSegmentFileName),
                    MinimalInit());
                WriteReadyMedia(staging, 0);
                WriteReadyMedia(staging, 1);
                return Task.CompletedTask;
            });

        var job = await StartRemuxJobAsync();
        await _manager.EnsureSegmentWillBeGeneratedAsync(job.JobId, -1, CreateSegments(20));

        var ffmpegTask = job.FfmpegTask;
        ffmpegTask.Should().NotBeNull();
        await ffmpegTask!.WaitAsync(TimeSpan.FromSeconds(5));

        HlsSegmentFileWaiter.IsInitReadyOnDisk(job.OutputDirectory).Should().BeTrue();
        HlsSegmentFileWaiter.IsSegmentReadyOnDisk(job.OutputDirectory, 0).Should().BeTrue();
    }

    private async Task<TranscodeJob> StartRemuxJobAsync()
    {
        return await _manager.GetOrStartJobAsync(
            Guid.NewGuid(),
            "input.mkv",
            "original",
            videoCodec: "copy",
            audioCodec: null,
            audioTrackIndex: 0,
            isAudioOnly: false,
            Guid.NewGuid());
    }

    private static List<HlsSegment> CreateSegments(int count)
    {
        var segments = new List<HlsSegment>(count);
        for (var i = 0; i < count; i++)
        {
            segments.Add(new HlsSegment
            {
                Number = i,
                StartTimestamp = i * 6000,
                Duration = 6000
            });
        }

        return segments;
    }

    private static void WriteReadyMedia(string directory, int index)
    {
        File.WriteAllBytes(
            Path.Combine(directory, index.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".m4s"),
            Concat(BuildMinimalMoof(), BuildBox("mdat", [0x02])));
    }

    private static byte[] MinimalInit() =>
        Concat(BuildBox("ftyp", [0x69, 0x73, 0x6F, 0x6D]), BuildBox("moov", new byte[24]));

    private static byte[] BuildMinimalMoof()
    {
        using var tfhdPayload = new MemoryStream();
        WriteUInt32(tfhdPayload, 0x020000u);
        WriteUInt32(tfhdPayload, 1);
        var tfhd = BuildBox("tfhd", tfhdPayload.ToArray());

        using var tfdtPayload = new MemoryStream();
        WriteUInt32(tfdtPayload, 0x0100_0000u);
        WriteUInt64(tfdtPayload, 0);
        var tfdt = BuildBox("tfdt", tfdtPayload.ToArray());

        using var trunPayload = new MemoryStream();
        WriteUInt32(trunPayload, 0x000001u | 0x000200u);
        WriteUInt32(trunPayload, 1);
        WriteInt32(trunPayload, 8);
        WriteUInt32(trunPayload, 1);
        var trun = BuildBox("trun", trunPayload.ToArray());

        var mfhd = BuildBox("mfhd", [0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01]);
        var traf = BuildBox("traf", Concat(tfhd, tfdt, trun));
        return BuildBox("moof", Concat(mfhd, traf));
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
        var length = parts.Sum(static p => p.Length);
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
