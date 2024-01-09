using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Interfaces;
using MediaServer.Tests.Helpers.Fixtures;

namespace MediaServer.Application.FunctionalTests.Services;

public class FileIndexerServiceTests : DatabaseAndFileFixture
{
    private readonly IFileIndexerService _fileIndexerService;

    public FileIndexerServiceTests(IFileIndexerService fileIndexerService)
    {
        _fileIndexerService = fileIndexerService;
    }

    [Test]
    public async Task IndexFiles()
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
        CancellationTokenSource cts = new();

        // Act
        await _fileIndexerService.IndexAsync(library, cts.Token);

        // Assert
        library.Files.Count().Should().Be(1);
    }
}
