using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using MediaServer.Application.Extensions;
using MediaServer.Application.UnitTests.Fixtures;
using MediaServer.Domain.Entities.Files;
using NUnit.Framework;

namespace MediaServer.Application.UnitTests.Extensions;

public class FileInfoExtensionsTests : FileFixture
{
    [TestCase(".weirdExtension", false)]
    [TestCase("", false)]
    [TestCase(null, false)]
    [TestCase(".mp3", true)]
    public void IsMediaFile_shouldWork(string? extension, bool expectedResult)
    {
        // Arrange
        var fileInfo = CreateTestFile($"file{extension}", "content");

        // Act
        var isMediaFile = fileInfo.IsMediaFile();

        // Assert
        isMediaFile.Should().Be(expectedResult);
    }

    [Test]
    public void ComputeFileHash_shouldWork()
    {
        // Arrange
        string content = "content";
        var fileInfo = CreateTestFile("computeFileHash.txt", content);
        var contentBytes = Encoding.UTF8.GetBytes(content);
        byte[] hashBytes = SHA256.HashData(contentBytes);
        var computedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLower();

        // Act
        var fileHash = fileInfo.ComputeFileHash();

        // Assert
        fileHash.Should().Be(computedHash);
    }

    [Test]
    public void ToMediaFile_shouldWork()
    {
        // Arrange
        var libraryId = 1;
        var fileInfo = CreateTestFile("computeFileHash.mp3", "content");
        MediaFile expectedMediaFile = new()
        {
            LibraryId = libraryId,
            Name = fileInfo.Name,
            Extension = fileInfo.Extension,
            Path = fileInfo.FullName,
            ParentDirectory = fileInfo.Directory?.Name,
            Hash = fileInfo.ComputeFileHash(),
            Size = fileInfo.Length
        };

        // Act
        var mediaFile = fileInfo.ToMediaFile(libraryId);

        // Assert
        mediaFile.Should().BeEquivalentTo(expectedMediaFile, options => options
            .Including(mf => mf.Name)
            .Including(mf => mf.Extension)
            .Including(mf => mf.Path)
            .Including(mf => mf.ParentDirectory)
            .Including(mf => mf.Hash));
    }

    [Test]
    public void ToMediaFile_shouldReturnNullMediaFile()
    {
        // Arrange
        var libraryId = 1;
        var fileInfo = CreateTestFile("computeFileHash.unkownExtension", "content");

        // Act
        var mediaFile = fileInfo.ToMediaFile(libraryId);

        // Assert
        mediaFile.Should().BeNull();
        mediaFile?.LibraryId.Should().Be(libraryId);
    }
}
