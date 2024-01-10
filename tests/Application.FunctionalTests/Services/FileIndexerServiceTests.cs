using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Interfaces;
using MediaServer.Infrastructure.Context.Data;
using MediaServer.Tests.Helpers.Fixtures;
using MediaServer.Tests.Helpers.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace MediaServer.Application.FunctionalTests.Services;

public class FileIndexerServiceTests : FileAndDatabaseFixture
{
    [Test]
    public /*async Task*/ void IndexFiles()
    {
        // Arrange
        //var fileIndexerService = scope.ServiceProvider.GetRequiredService<IFileIndexerService>();
        FileHelper.CreateTestFile("test.mp3", "content");
        FileHelper.CreateTestFile("ignored.extension", "content");
        var library = new Library()
        {
            MediaType = LibraryMediaType.Music,
            RootPath = FileHelper.TestDirectoryPath, // "C:\\Users\\Kaybi\\Documents\\Workspace\\media",
            Title = "Title"
        };
        CancellationTokenSource cts = new();

        // Act
        //await fileIndexerService.IndexAsync(library, cts.Token);

        // Assert
        library.Files.Count().Should().Be(1);
    }
}
