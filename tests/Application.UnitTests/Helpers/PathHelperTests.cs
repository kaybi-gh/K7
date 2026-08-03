using K7.Server.Application.Helpers;

namespace K7.Server.Application.UnitTests.Helpers;

public class PathHelperTests
{
    [Test]
    [Platform("Win")]
    public void IsPathInScope_ShouldMatchExactFilePath_Windows()
    {
        var file = @"C:\music\artist\album\01-track.flac";
        var scope = @"C:\music\artist\album\01-track.flac";

        PathHelper.IsPathInScope(file, scope).Should().BeTrue();
    }

    [Test]
    [Platform("Unix")]
    public void IsPathInScope_ShouldMatchExactFilePath_Unix()
    {
        var file = "/music/artist/album/01-track.flac";
        var scope = "/music/artist/album/01-track.flac";

        PathHelper.IsPathInScope(file, scope).Should().BeTrue();
    }

    [Test]
    [Platform("Win")]
    public void IsPathInScope_ShouldMatchNestedFileUnderDirectoryScope_Windows()
    {
        var file = @"C:\music\artist\album\01-track.flac";
        var scope = @"C:\music\artist\album";

        PathHelper.IsPathInScope(file, scope).Should().BeTrue();
    }

    [Test]
    [Platform("Unix")]
    public void IsPathInScope_ShouldMatchNestedFileUnderDirectoryScope_Unix()
    {
        var file = "/music/artist/album/01-track.flac";
        var scope = "/music/artist/album";

        PathHelper.IsPathInScope(file, scope).Should().BeTrue();
    }

    [Test]
    [Platform("Win")]
    public void IsPathInScope_ShouldNotMatchSiblingDirectoryPrefix_Windows()
    {
        var file = @"C:\music\artist\album-extra\01-track.flac";
        var scope = @"C:\music\artist\album";

        PathHelper.IsPathInScope(file, scope).Should().BeFalse();
    }

    [Test]
    [Platform("Unix")]
    public void IsPathInScope_ShouldNotMatchSiblingDirectoryPrefix_Unix()
    {
        var file = "/music/artist/album-extra/01-track.flac";
        var scope = "/music/artist/album";

        PathHelper.IsPathInScope(file, scope).Should().BeFalse();
    }

    [Test]
    [Platform("Win")]
    public void IsPathInScope_ShouldTreatMixedSeparatorsAsEqual_Windows()
    {
        var file = @"C:/music/artist/album/01-track.flac";
        var scope = @"C:\music\artist\album\01-track.flac";

        PathHelper.IsPathInScope(file, scope).Should().BeTrue();
    }

    [Test]
    [Platform("Win")]
    public void NormalizeLibraryPath_ShouldResolveRelativePathAgainstLibraryRoot_Windows()
    {
        var normalized = PathHelper.NormalizeLibraryPath(@"artist\album\track.flac", @"C:\music");

        normalized.Should().Be(PathHelper.NormalizePath(@"C:\music\artist\album\track.flac"));
    }

    [Test]
    [Platform("Unix")]
    public void NormalizeLibraryPath_ShouldResolveRelativePathAgainstLibraryRoot_Unix()
    {
        var normalized = PathHelper.NormalizeLibraryPath("artist/album/track.flac", "/music");

        normalized.Should().Be(PathHelper.NormalizePath("/music/artist/album/track.flac"));
    }

    [Test]
    [Platform("Win")]
    public void NormalizeLibraryPath_ShouldKeepAbsolutePathUnchanged_Windows()
    {
        var path = @"C:\music\artist\album\track.flac";
        var normalized = PathHelper.NormalizeLibraryPath(path, @"D:\other-root");

        normalized.Should().Be(PathHelper.NormalizePath(path));
    }

    [Test]
    [Platform("Unix")]
    public void NormalizeLibraryPath_ShouldKeepAbsolutePathUnchanged_Unix()
    {
        var path = "/music/artist/album/track.flac";
        var normalized = PathHelper.NormalizeLibraryPath(path, "/other-root");

        normalized.Should().Be(PathHelper.NormalizePath(path));
    }

    [Test]
    [Platform("Win")]
    public void GetContainingDirectoryPath_ShouldReturnParent_Windows()
    {
        var path = @"C:\media\series\Cool Show\Season 01\S01E01.mkv";

        PathHelper.GetContainingDirectoryPath(path).Should().Be(@"C:\media\series\Cool Show\Season 01");
    }

    [Test]
    [Platform("Unix")]
    public void GetContainingDirectoryPath_ShouldReturnParent_Unix()
    {
        var path = "/media/series/Cool Show/Season 01/S01E01.mkv";

        PathHelper.GetContainingDirectoryPath(path).Should().Be("/media/series/Cool Show/Season 01");
    }

    [Test]
    [Platform("Win")]
    public void IsInContainingDirectory_ShouldDistinguishSameFolderName_Windows()
    {
        var left = @"C:\media\series\Cool Show\Season 01\S01E01.mkv";
        var right = @"C:\media\series\Other Show\Season 01\S01E01.mkv";
        var coolSeason = @"C:\media\series\Cool Show\Season 01";

        PathHelper.IsInContainingDirectory(left, coolSeason).Should().BeTrue();
        PathHelper.IsInContainingDirectory(right, coolSeason).Should().BeFalse();
        PathHelper.ContainingDirectoriesEqual(left, right).Should().BeFalse();
    }

    [Test]
    [Platform("Unix")]
    public void IsInContainingDirectory_ShouldDistinguishSameFolderName_Unix()
    {
        var left = "/media/series/Cool Show/Season 01/S01E01.mkv";
        var right = "/media/series/Other Show/Season 01/S01E01.mkv";
        var coolSeason = "/media/series/Cool Show/Season 01";

        PathHelper.IsInContainingDirectory(left, coolSeason).Should().BeTrue();
        PathHelper.IsInContainingDirectory(right, coolSeason).Should().BeFalse();
        PathHelper.ContainingDirectoriesEqual(left, right).Should().BeFalse();
    }

    [Test]
    public void GetContainingDirectoryPath_ShouldReturnNull_WhenPathMissing()
    {
        PathHelper.GetContainingDirectoryPath(null).Should().BeNull();
        PathHelper.GetContainingDirectoryPath("").Should().BeNull();
    }
}
