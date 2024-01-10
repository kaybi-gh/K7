using MediaServer.Application.Features.Libraries.Commands.CreateLibrary;
using MediaServer.Application.Features.Libraries.Commands.IndexLibraryFiles;
using MediaServer.Application.Features.Libraries.Queries.GetLibrariesFiles;
using MediaServer.Domain.Enums;
using MediaServer.Tests.Helpers.Fixtures;
using MediaServer.Tests.Helpers.Helpers;

namespace MediaServer.Application.FunctionalTests.Libraries.Commands;

public class IndexLibraryFilesTests : FileAndDatabaseFixture
{
    [Test]
    public async Task ShouldRequireValidLibraryId()
    {
        // Arrange

        // Act
        var command = new IndexLibraryFilesCommand(99);

        // Assert
        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldIndexLibraryFiles()
    {
        // Arrange
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = FileHelper.TestDirectoryPath
        });
        FileHelper.CreateTestFile("movie.mp4", "movie");
        FileHelper.CreateTestFile("movie.mkv", "movie");
        FileHelper.CreateTestFile("other.unkown", "other");

        // Act
        await SendAsync(new IndexLibraryFilesCommand(libraryId));
        var libraryIndexedFiles = await SendAsync(new GetLibraryIndexedFilesWithPaginationQuery()
        {
            LibraryId = libraryId
        });

        // Assert
        libraryIndexedFiles.TotalCount.Should().Be(2);
    }
}
