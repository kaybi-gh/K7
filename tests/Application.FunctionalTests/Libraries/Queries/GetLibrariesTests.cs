using MediaServer.Application.FunctionalTests.Fixtures;
using MediaServer.Application.Libraries.Commands.CreateLibrary;
using MediaServer.Application.Libraries.Queries.GetLibraries;
using MediaServer.Domain.Enums;

namespace MediaServer.Application.FunctionalTests.Libraries.Queries;

public class GetLibrariesTests : BaseTestFixture
{
    [Test]
    public async Task ShouldReturnLibraries()
    {
        // Arrange
        await RunAsDefaultUserAsync();
        await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/root/path"
        });
        var query = new GetLibrariesQuery();

        // Act
        var result = await SendAsync(query);

        // Assert
        result.Should().HaveCount(1);
    }

    [Test]
    public async Task ShouldDenyAnonymousUser()
    {
        // Arrange
        var query = new GetLibrariesQuery();

        // Act
        var action = () => SendAsync(query);

        // Assert
        await action.Should().ThrowAsync<UnauthorizedAccessException>();
    }
}
