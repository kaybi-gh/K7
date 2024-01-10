using MediaServer.Application.Helpers;
using MediaServer.Tests.Helpers.Fixtures;
using MediaServer.Tests.Helpers.Helpers;

namespace MediaServer.Application.UnitTests.Helper;

public class FileInfoHelperTests : FileFixture
{
    [Test]
    public void GetAllFilesRecursively_ReturnsAllFiles()
    {
        // Arrange
        List<FileInfo> expectedFiles = FileHelper.CreateTestFiles();

        // Act
        List<FileInfo> actualFiles = FileInfoHelper.GetAllFileInfosRecursively(FileHelper.TestDirectoryPath);

        // Assert
        actualFiles.Should().BeEquivalentTo(expectedFiles, options => options
            .Including(fi => fi.FullName)
            .Including(fi => fi.Extension)
            .Including(fi => fi.Name)
            .Including(fi => fi.CreationTime));
    }
}
