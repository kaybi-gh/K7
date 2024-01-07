using FluentAssertions;
using MediaServer.Application.Helper;
using MediaServer.Application.UnitTests.Fixtures;
using NUnit.Framework;

namespace MediaServer.Application.UnitTests.Helper;

public class FileInfoHelperTests : FileFixture
{
    [Test]
    public void GetAllFilesRecursively_ReturnsAllFiles()
    {
        // Arrange
        List<FileInfo> expectedFiles = CreateTestFiles();

        // Act
        List<FileInfo> actualFiles = FileInfoHelper.GetAllFileInfosRecursively(TestDirectoryPath);

        // Assert
        actualFiles.Should().BeEquivalentTo(expectedFiles, options => options
            .Including(fi => fi.FullName)
            .Including(fi => fi.Extension)
            .Including(fi => fi.Name)
            .Including(fi => fi.CreationTime));
    }
}
