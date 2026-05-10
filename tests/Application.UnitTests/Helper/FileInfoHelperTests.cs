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

        // Act
        var (files, inaccessiblePaths) = FileInfoHelper.GetAllFileInfosRecursively(FileHelper.TestDirectoryPath);

        // Assert
        files.Should().NotContain(f => f.FullName.Contains("@eaDir"));
        files.Should().NotContain(f => f.FullName.Contains(".synology"));
        files.Should().NotContain(f => f.FullName.Contains("#recycle"));
        files.Should().NotContain(f => f.FullName.Contains(".Trash-1000"));
        inaccessiblePaths.Should().BeEmpty();
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
