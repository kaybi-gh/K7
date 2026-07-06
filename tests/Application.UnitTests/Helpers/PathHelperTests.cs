using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

public class PathHelperTests
{
    [Test]
    public void IsPathInScope_ShouldMatchExactFilePath()
    {
        var file = @"C:\music\artist\album\01-track.flac";
        var scope = @"C:\music\artist\album\01-track.flac";

        PathHelper.IsPathInScope(file, scope).Should().BeTrue();
    }

    [Test]
    public void IsPathInScope_ShouldMatchNestedFileUnderDirectoryScope()
    {
        var file = @"C:\music\artist\album\01-track.flac";
        var scope = @"C:\music\artist\album";

        PathHelper.IsPathInScope(file, scope).Should().BeTrue();
    }

    [Test]
    public void IsPathInScope_ShouldNotMatchSiblingDirectoryPrefix()
    {
        var file = @"C:\music\artist\album-extra\01-track.flac";
        var scope = @"C:\music\artist\album";

        PathHelper.IsPathInScope(file, scope).Should().BeFalse();
    }

    [Test]
    public void IsPathInScope_ShouldTreatMixedSeparatorsAsEqual()
    {
        var file = @"C:/music/artist/album/01-track.flac";
        var scope = @"C:\music\artist\album\01-track.flac";

        PathHelper.IsPathInScope(file, scope).Should().BeTrue();
    }

    [Test]
    public void NormalizePath_ShouldResolveRelativePathAgainstRoot()
    {
        var normalized = PathHelper.NormalizePath(@"artist\album\track.flac", @"C:\music");

        normalized.Should().Be(PathHelper.NormalizePath(@"C:\music\artist\album\track.flac"));
    }
}
