using K7.Server.Infrastructure.MediaProcessing;

namespace K7.Server.Application.UnitTests.Infrastructure.MediaProcessing;

[TestFixture]
public class RemuxSegmentPromoterTests
{
    [Test]
    public void TryPromoteMediaSegment_ShouldNotOverwriteReadySharedFile()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7-remux-promote-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(root, "head-1");
        var shared = Path.Combine(root, "shared");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(shared);

        try
        {
            File.WriteAllBytes(Path.Combine(staging, "3.m4s"), new byte[64]);
            File.WriteAllBytes(Path.Combine(shared, "3.m4s"), new byte[96]);

            RemuxSegmentPromoter.TryPromoteMediaSegment(staging, shared, 3).Should().BeFalse();
            new FileInfo(Path.Combine(shared, "3.m4s")).Length.Should().Be(96);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void IsStagingSegmentClosed_ShouldRequireNextFile_WhileFfmpegRuns()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7-remux-promote-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(root, "head-1");
        Directory.CreateDirectory(staging);

        try
        {
            // 5.m4s may still receive a second fragment (collapsed interior keyframe).
            File.WriteAllBytes(Path.Combine(staging, "5.m4s"), new byte[64]);
            RemuxSegmentPromoter.IsStagingSegmentClosed(staging, 5, ffmpegExited: false).Should().BeFalse();

            // The segment muxer opened 6.m4s: 5.m4s is closed.
            File.WriteAllBytes(Path.Combine(staging, "6.m4s"), new byte[1]);
            RemuxSegmentPromoter.IsStagingSegmentClosed(staging, 5, ffmpegExited: false).Should().BeTrue();
            RemuxSegmentPromoter.IsStagingSegmentClosed(staging, 6, ffmpegExited: false).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void IsStagingSegmentClosed_ShouldAcceptLastFile_AfterFfmpegExit()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7-remux-promote-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(root, "head-1");
        Directory.CreateDirectory(staging);

        try
        {
            File.WriteAllBytes(Path.Combine(staging, "9.m4s"), new byte[64]);
            RemuxSegmentPromoter.IsStagingSegmentClosed(staging, 9, ffmpegExited: true).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void TryPromoteMediaSegment_ShouldPromoteWhenSharedMissing()
    {
        var root = Path.Combine(Path.GetTempPath(), "k7-remux-promote-" + Guid.NewGuid().ToString("N"));
        var staging = Path.Combine(root, "head-1");
        var shared = Path.Combine(root, "shared");
        Directory.CreateDirectory(staging);
        Directory.CreateDirectory(shared);

        try
        {
            File.WriteAllBytes(Path.Combine(staging, "2.m4s"), new byte[80]);

            RemuxSegmentPromoter.TryPromoteMediaSegment(staging, shared, 2).Should().BeTrue();
            new FileInfo(Path.Combine(shared, "2.m4s")).Length.Should().Be(80);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
