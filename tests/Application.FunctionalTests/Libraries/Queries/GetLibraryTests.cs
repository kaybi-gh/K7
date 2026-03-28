using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Application.Features.Libraries.Queries.GetLibrary;
using K7.Server.Domain.Enums;
using K7.Tests.Helpers.Fixtures;

namespace K7.Server.Application.FunctionalTests.Libraries.Queries;

public class GetLibraryTests : DatabaseFixture
{
    [Test]
    public async Task ShouldRequireValidLibraryId()
    {
        // Arrange
        await RunAsDefaultUserAsync();

        // Act
        var query = new GetLibraryQuery(Guid.NewGuid());

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
            MetadataProviderName = "tmdb",
            RootPath = "/root/path"
        });

        // Act
        var library = await SendAsync(new GetLibraryQuery(libraryId));

        // Assert
        library.Should().NotBeNull();
    }
}
