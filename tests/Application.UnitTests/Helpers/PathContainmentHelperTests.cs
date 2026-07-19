using K7.Server.Application.Common.Security;

namespace K7.Server.Application.UnitTests.Helpers;

public class PathContainmentHelperTests
{
    [Test]
    public void IsPathContained_ShouldReturnTrue_ForExactRootAndChild()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "k7-root-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            var child = Path.Combine(root, "movies", "a.mkv");

            PathContainmentHelper.IsPathContained(root, [root]).Should().BeTrue();
            PathContainmentHelper.IsPathContained(child, [root]).Should().BeTrue();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Test]
    public void IsPathContained_ShouldReturnFalse_WhenOutsideAllowedRoots()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "k7-root-" + Guid.NewGuid().ToString("N")));
        var other = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "k7-other-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(other);
        try
        {
            PathContainmentHelper.IsPathContained(Path.Combine(other, "x.mkv"), [root]).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(other, recursive: true);
        }
    }

    [Test]
    public void IsPathContained_ShouldIgnoreBlankRoots()
    {
        var candidate = Path.GetTempPath();

        PathContainmentHelper.IsPathContained(candidate, ["", "  ", null!]).Should().BeFalse();
    }

    [Test]
    [Platform("Unix")]
    public void IsPathContained_ShouldMatchChildren_WhenAllowedRootIsUnixFilesystemRoot()
    {
        PathContainmentHelper.IsPathContained("/", ["/"]).Should().BeTrue();
        PathContainmentHelper.IsPathContained("/media/Animes", ["/"]).Should().BeTrue();
        PathContainmentHelper.IsPathContained("/media/Series", ["/"]).Should().BeTrue();
    }

    [Test]
    public void EnsurePathContained_ShouldThrow_WhenNotContained()
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "k7-root-" + Guid.NewGuid().ToString("N")));
        var other = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "k7-other-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(other);
        try
        {
            var act = () => PathContainmentHelper.EnsurePathContained(
                Path.Combine(other, "x.mkv"),
                [root],
                "denied");

            act.Should().Throw<UnauthorizedAccessException>().WithMessage("denied");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            Directory.Delete(other, recursive: true);
        }
    }
}
