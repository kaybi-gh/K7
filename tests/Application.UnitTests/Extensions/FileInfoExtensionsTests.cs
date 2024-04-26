using MediaServer.Application.Extensions;
using MediaServer.Domain.Entities;
using MediaServer.Tests.Helpers.Fixtures;
using MediaServer.Tests.Helpers.Helpers;

namespace MediaServer.Application.UnitTests.Extensions;

public class FileInfoExtensionsTests : FileFixture
{
    [TestCase(".unkownExtension", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase(".mp3", true)]
    public void ShouldReturnCorrectIsSupportedFile(string? extension, bool expectedResult)
    {
        // Arrange
        var fileInfo = FileHelper.CreateTestFile($"file{extension}", "content");

        // Act
        var isMediaFile = fileInfo.IsSupportedFile();

        // Assert
        isMediaFile.Should().Be(expectedResult);
    }

    [Test]
    public void ShouldComputeFileHash()
    {
        // Arrange
        string content = "content";
        var fileInfo = FileHelper.CreateTestFile("computeFileSeed.txt", content);

        // Act
        var fileHash = fileInfo.ComputeFileHash();

        // Assert
        fileHash.Should().BeGreaterThan(0);
    }

    [Test]
    public void ShouldConvertToIndexedFile()
    {
        // Arrange
        var libraryId = Guid.NewGuid();
        var fileInfo = FileHelper.CreateTestFile("file.mkv", "content");
        IndexedFile expectedIndexedFile = new()
        {
            LibraryId = libraryId,
            Name = "file",
            Extension = ".mkv",
            Path = Path.Combine(FileHelper.TestDirectoryPath, "file.mkv"),
            ParentDirectory = fileInfo.Directory?.Name,
            Hash = fileInfo.ComputeFileHash(),
            Size = fileInfo.Length
        };

        // Act
        var indexedFile = fileInfo.ToIndexedFile(libraryId);

        // Assert
        indexedFile.Should().BeEquivalentTo(expectedIndexedFile);
    }

    [Test]
    public void ShouldNotConvertToIndexedFile()
    {
        // Arrange
        var libraryId = Guid.NewGuid();
        var fileInfo = FileHelper.CreateTestFile("file.unkownExtension", "content");

        // Act
        var indexedFile = fileInfo.ToIndexedFile(libraryId);

        // Assert
        indexedFile.Should().BeNull();
        indexedFile?.LibraryId.Should().Be(libraryId);
    }
}
