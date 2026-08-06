using FluentAssertions;
using K7.Server.Application.Features.OpenSubsonic;

namespace K7.Server.Application.UnitTests.Features.OpenSubsonic;

[TestFixture]
public class OpenSubsonicStreamTranscodeTests
{
    [Test]
    public void TryResolve_ShouldReturnFalse_WhenDownload()
    {
        var ok = OpenSubsonicStreamTranscode.TryResolve(
            download: true,
            format: "mp3",
            maxBitRateKbps: 128,
            timeOffsetSeconds: 0,
            sourceExtension: ".flac",
            fileSizeBytes: 40_000_000,
            durationSeconds: 200,
            out _,
            out _);

        ok.Should().BeFalse();
    }

    [Test]
    public void TryResolve_ShouldReturnFalse_WhenRawFormat()
    {
        var ok = OpenSubsonicStreamTranscode.TryResolve(
            download: false,
            format: "raw",
            maxBitRateKbps: 128,
            timeOffsetSeconds: 0,
            sourceExtension: ".flac",
            fileSizeBytes: 40_000_000,
            durationSeconds: 200,
            out _,
            out _);

        ok.Should().BeFalse();
    }

    [Test]
    public void TryResolve_ShouldTranscodeFlac_WhenFormatMp3()
    {
        var ok = OpenSubsonicStreamTranscode.TryResolve(
            download: false,
            format: "mp3",
            maxBitRateKbps: null,
            timeOffsetSeconds: 0,
            sourceExtension: ".flac",
            fileSizeBytes: 40_000_000,
            durationSeconds: 200,
            out var format,
            out var bitrate);

        ok.Should().BeTrue();
        format.Should().Be("mp3");
        bitrate.Should().Be(192);
    }

    [Test]
    public void TryResolve_ShouldTranscodeLossless_WhenMaxBitRateSet()
    {
        var ok = OpenSubsonicStreamTranscode.TryResolve(
            download: false,
            format: null,
            maxBitRateKbps: 128,
            timeOffsetSeconds: 10,
            sourceExtension: ".flac",
            fileSizeBytes: 40_000_000,
            durationSeconds: 200,
            out var format,
            out var bitrate);

        ok.Should().BeTrue();
        format.Should().Be("mp3");
        bitrate.Should().Be(128);
    }

    [Test]
    public void TryResolve_ShouldReturnFalse_WhenMp3AlreadyFits()
    {
        // ~128 kbps mp3: 128000/8 * 200 = 3_200_000 bytes
        var ok = OpenSubsonicStreamTranscode.TryResolve(
            download: false,
            format: null,
            maxBitRateKbps: 192,
            timeOffsetSeconds: 0,
            sourceExtension: ".mp3",
            fileSizeBytes: 3_200_000,
            durationSeconds: 200,
            out _,
            out _);

        ok.Should().BeFalse();
    }

    [Test]
    public void TryResolve_ShouldTranscode_WhenBitrateExceedsMax()
    {
        // ~320 kbps over 128 max
        var ok = OpenSubsonicStreamTranscode.TryResolve(
            download: false,
            format: null,
            maxBitRateKbps: 128,
            timeOffsetSeconds: 0,
            sourceExtension: ".mp3",
            fileSizeBytes: 8_000_000,
            durationSeconds: 200,
            out var format,
            out var bitrate);

        ok.Should().BeTrue();
        format.Should().Be("mp3");
        bitrate.Should().Be(128);
    }
}
