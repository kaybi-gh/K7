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
