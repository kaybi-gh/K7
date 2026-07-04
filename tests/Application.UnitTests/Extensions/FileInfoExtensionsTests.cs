using K7.Server.Application.Extensions;
using K7.Server.Domain.Entities;
using K7.Tests.Helpers.Fixtures;
using K7.Tests.Helpers.Helpers;

namespace K7.Server.Application.UnitTests.Extensions;

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
            Size = fileInfo.Length,
            LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
        };

        // Act
        var indexedFile = fileInfo.ToIndexedFile(libraryId);

        // Assert
        indexedFile.Should().BeEquivalentTo(expectedIndexedFile, options => options.Excluding(x => x.Id));
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
