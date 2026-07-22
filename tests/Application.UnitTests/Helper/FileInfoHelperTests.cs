using K7.Server.Application.Helpers;
using K7.Tests.Helpers.Fixtures;
using K7.Tests.Helpers.Helpers;

namespace K7.Server.Application.UnitTests.Helper;

public class FileInfoHelperTests : FileFixture
{
    [Test]
    public void GetAllFilesRecursively_ShouldReturnAllFiles()
    {
        // Arrange
        List<FileInfo> expectedFiles = FileHelper.CreateTestFiles();

        // Act
        var (actualFiles, inaccessiblePaths) = FileInfoHelper.GetAllFileInfosRecursively(FileHelper.TestDirectoryPath);

        // Assert
        actualFiles.Should().BeEquivalentTo(expectedFiles, options => options
            .Including(fi => fi.FullName)
            .Including(fi => fi.Extension)
            .Including(fi => fi.Name)
            .Including(fi => fi.CreationTime));
        inaccessiblePaths.Should().BeEmpty();
    }

    [Test]
    public void GetAllFilesRecursively_ShouldThrow_WhenRootDirectoryDoesNotExist()
    {
        // Arrange
        var nonExistentPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());

        // Act
        var act = () => FileInfoHelper.GetAllFileInfosRecursively(nonExistentPath);

        // Assert
        act.Should().Throw<DirectoryNotFoundException>();
    }

    [Test]
    public void GetAllFilesRecursively_ShouldSkipExcludedNasDirectories()
    {
        // Arrange
        FileHelper.CreateTestFiles();
        var eaDirPath = Path.Combine(FileHelper.TestDirectoryPath, "@eaDir");
        Directory.CreateDirectory(eaDirPath);
        File.WriteAllText(Path.Combine(eaDirPath, "hidden.txt"), "nas metadata");

        var synologyPath = Path.Combine(FileHelper.TestDirectoryPath, ".synology");
        Directory.CreateDirectory(synologyPath);
        File.WriteAllText(Path.Combine(synologyPath, "config.dat"), "synology config");

        var recyclePath = Path.Combine(FileHelper.TestDirectoryPath, "#recycle");
        Directory.CreateDirectory(recyclePath);
        File.WriteAllText(Path.Combine(recyclePath, "deleted.mp3"), "recycled");

        var trashPath = Path.Combine(FileHelper.TestDirectoryPath, ".Trash-1000");
        Directory.CreateDirectory(trashPath);
        File.WriteAllText(Path.Combine(trashPath, "trashed.mp3"), "trashed");

        var thumbPath = Path.Combine(FileHelper.TestDirectoryPath, ".@__thumb");
        Directory.CreateDirectory(thumbPath);
        File.WriteAllText(Path.Combine(thumbPath, "thumb.jpg"), "thumb");

        var tmpPath = Path.Combine(FileHelper.TestDirectoryPath, "@tmp");
        Directory.CreateDirectory(tmpPath);
        File.WriteAllText(Path.Combine(tmpPath, "tmp.dat"), "tmp");

        // Act
        var (files, inaccessiblePaths) = FileInfoHelper.GetAllFileInfosRecursively(FileHelper.TestDirectoryPath);

        // Assert
        files.Should().NotContain(f => f.FullName.Contains("@eaDir"));
        files.Should().NotContain(f => f.FullName.Contains(".synology"));
        files.Should().NotContain(f => f.FullName.Contains("#recycle"));
        files.Should().NotContain(f => f.FullName.Contains(".Trash-1000"));
        files.Should().NotContain(f => f.FullName.Contains(".@__thumb"));
        files.Should().NotContain(f => f.FullName.Contains("@tmp"));
        inaccessiblePaths.Should().BeEmpty();
    }

    [Test]
    public void GetAllFilesRecursively_ShouldReturnEmpty_WhenRootIsExcludedDirectory()
    {
        // Arrange
        var eaDirPath = Path.Combine(FileHelper.TestDirectoryPath, "@eaDir");
        Directory.CreateDirectory(eaDirPath);
        File.WriteAllText(Path.Combine(eaDirPath, "hidden.txt"), "nas metadata");

        // Act
        var (files, inaccessiblePaths) = FileInfoHelper.GetAllFileInfosRecursively(eaDirPath);

        // Assert
        files.Should().BeEmpty();
        inaccessiblePaths.Should().BeEmpty();
    }

    [Test]
    public void GetSupportedFilesForPaths_ShouldSkipExcludedPaths()
    {
        // Arrange
        FileHelper.CreateTestFiles();
        var mediaPath = Path.Combine(FileHelper.TestDirectoryPath, "song.mp3");
        File.WriteAllText(mediaPath, "audio");
        var eaDirPath = Path.Combine(FileHelper.TestDirectoryPath, "@eaDir");
        Directory.CreateDirectory(eaDirPath);
        File.WriteAllText(Path.Combine(eaDirPath, "track.mp3"), "hidden audio");

        // Act
        var (files, inaccessiblePaths) = FileInfoHelper.GetSupportedFilesForPaths(
            [mediaPath, eaDirPath, Path.Combine(eaDirPath, "track.mp3")]);

        // Assert
        files.Should().ContainSingle(f => f.Path == mediaPath);
        files.Should().NotContain(f => f.Path.Contains("@eaDir", StringComparison.OrdinalIgnoreCase));
        inaccessiblePaths.Should().BeEmpty();
    }

    [TestCase("@eaDir", true)]
    [TestCase(".@__thumb", true)]
    [TestCase("@tmp", true)]
    [TestCase(".Trash-1000", true)]
    [TestCase("Music", false)]
    public void IsExcludedDirectoryName_ShouldMatchNasFolders(string name, bool expected)
    {
        FileInfoHelper.IsExcludedDirectoryName(name).Should().Be(expected);
    }

    [Test]
    public void IsExcludedPath_ShouldDetectExcludedSegment()
    {
        var path = Path.Combine("library", "Artist", "@eaDir", "SYNOFILE.db");

        FileInfoHelper.IsExcludedPath(path).Should().BeTrue();
        FileInfoHelper.IsExcludedPath(Path.Combine("library", "Artist", "Album")).Should().BeFalse();
    }

    [Test]
    [Platform("Linux,MacOsX")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility")]
    public void GetAllFilesRecursively_ShouldCollectInaccessiblePaths_WhenPermissionDenied()
    {
        // Arrange
        FileHelper.CreateTestFiles();
        var restrictedDir = Path.Combine(FileHelper.TestDirectoryPath, "restricted");
        Directory.CreateDirectory(restrictedDir);
        File.WriteAllText(Path.Combine(restrictedDir, "secret.txt"), "secret");

        // Remove read+execute permissions on the directory (Unix only)
        var dirInfo = new DirectoryInfo(restrictedDir);
        dirInfo.UnixFileMode = UnixFileMode.None;

        // Act
        var (files, inaccessiblePaths) = FileInfoHelper.GetAllFileInfosRecursively(FileHelper.TestDirectoryPath);

        // Assert
        files.Should().NotContain(f => f.FullName.Contains("restricted"));
        inaccessiblePaths.Should().ContainSingle()
            .Which.Path.Should().Be(restrictedDir);

        // Cleanup: restore permissions so teardown can delete
        dirInfo.UnixFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    }
}
