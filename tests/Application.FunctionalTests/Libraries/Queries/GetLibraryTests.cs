using MediaServer.Application.Features.Libraries.Commands.CreateLibrary;
using MediaServer.Application.Features.Libraries.Queries.GetLibrary;
using MediaServer.Domain.Enums;
using MediaServer.Tests.Helpers.Fixtures;

namespace MediaServer.Application.FunctionalTests.Libraries.Queries;

public class GetLibraryTests : DatabaseFixture
{
    [Test]
    public async Task ShouldRequireValidLibraryId()
    {
        // Arrange
        await RunAsDefaultUserAsync();

        // Act
        var query = new GetLibraryQuery(99);

        // Assert
        await FluentActions.Invoking(() => SendAsync(query)).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldGetLibrary()
    {
        // Arrange
        await RunAsDefaultUserAsync();
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/root/path"
        });

        // Act
        var library = await SendAsync(new GetLibraryQuery(libraryId));

        // Assert
        library.Should().NotBeNull();
    }
}
