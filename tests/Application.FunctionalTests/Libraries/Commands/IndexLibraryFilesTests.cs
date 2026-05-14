using K7.Server.Application.Features.IndexedFiles.Queries.GetIndexedFiles;
using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Domain.Enums;
using K7.Tests.Helpers.Fixtures;
using K7.Tests.Helpers.Helpers;

namespace K7.Server.Application.FunctionalTests.Libraries.Commands;

public class IndexLibraryFilesTests : FileAndDatabaseFixture
{
    [Test]
    public async Task ShouldRequireValidLibraryId()
    {
        // Arrange

        // Act
        var command = new IndexLibraryFilesCommand(Guid.NewGuid());

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
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = FileHelper.TestDirectoryPath
        });
        FileHelper.CreateTestFile("movie.mp4", "movie");
        FileHelper.CreateTestFile("movie.mkv", "movie");
        FileHelper.CreateTestFile("other.unkown", "other");

        // Act
        await SendAsync(new IndexLibraryFilesCommand(libraryId));
        var libraryIndexedFiles = await SendAsync(new GetIndexedFilesWithPaginationQuery()
        {
            LibraryId = libraryId
        });

        // Assert
        libraryIndexedFiles.TotalCount.Should().Be(2);
    }
}
