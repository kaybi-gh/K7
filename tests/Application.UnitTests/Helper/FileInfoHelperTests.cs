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
        List<FileInfo> actualFiles = FileInfoHelper.GetAllFileInfosRecursively(FileHelper.TestDirectoryPath);

        // Assert
        actualFiles.Should().BeEquivalentTo(expectedFiles, options => options
            .Including(fi => fi.FullName)
            .Including(fi => fi.Extension)
            .Including(fi => fi.Name)
            .Including(fi => fi.CreationTime));
    }
}
