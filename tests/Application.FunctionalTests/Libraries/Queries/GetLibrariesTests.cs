using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Application.Features.Libraries.Queries.GetLibraries;
using K7.Server.Domain.Enums;
using K7.Tests.Helpers.Fixtures;

namespace K7.Server.Application.FunctionalTests.Libraries.Queries;

public class GetLibrariesTests : DatabaseFixture
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
