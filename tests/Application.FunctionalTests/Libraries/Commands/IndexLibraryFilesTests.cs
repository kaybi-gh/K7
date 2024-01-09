using MediaServer.Application.Features.Libraries.Commands.CreateLibrary;
using MediaServer.Application.Features.Libraries.Commands.IndexLibraryFiles;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Tests.Helpers.Fixtures;

namespace MediaServer.Application.FunctionalTests.Libraries.Commands;

public class IndexLibraryFilesTests : DatabaseFixture
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
    public async Task ShouldDeleteLibrary()
    {
        // Arrange
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/root/path"
        });

        // Act
        await SendAsync(new IndexLibraryFilesCommand(libraryId));
        var library = await FindAsync<Library>(libraryId);

        // Assert
        library?.Files.Should().NotBeEmpty();
    }
}
