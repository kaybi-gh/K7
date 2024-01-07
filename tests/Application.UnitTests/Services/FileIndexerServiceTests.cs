using FluentAssertions;
using MediaServer.Application.Services;
using MediaServer.Application.UnitTests.Fixtures;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using NUnit.Framework;

namespace MediaServer.Application.UnitTests.Services;

public class FileIndexerServiceTests : FileFixture
{
    private static readonly FileIndexerService FileIndexerService = new();

    [Test]
    public void IndexMediaFiles()
    {
        // Arrange
        CreateTestFile("test.mp3", "content");
        CreateTestFile("ignored.extension", "content");
        var library = new Library()
        {
            MediaType = LibraryMediaType.Music,
            RootPath = TestDirectoryPath, // "C:\\Users\\Kaybi\\Documents\\Workspace\\media",
            Title = "Title"
        };

        // Act
        FileIndexerService.IndexMediaFiles(library);

        // Assert
        library.Files.Count.Should().Be(1);
    }
}
