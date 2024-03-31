using MediaServer.Application.Common.Exceptions;
using MediaServer.Application.Features.Libraries.Commands.CreateLibrary;
using MediaServer.Application.Features.Libraries.Commands.UpdateLibrary;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Tests.Helpers.Fixtures;

namespace MediaServer.Application.FunctionalTests.Libraries.Commands;

public class UpdateLibraryListTests : DatabaseFixture
{
    [Test]
    public async Task ShouldRequireValidLibraryId()
    {
        // Arrange
        var command = new UpdateLibraryCommand
        {
            Id = 99,
            Title = "New Title"
        };

        // Act
        // Assert
        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldRequireUniqueTitle()
    {
        // Arrange
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/root/path"
        });
        await SendAsync(new CreateLibraryCommand
        {
            Title = "Other Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/root/path"
        });
        var command = new UpdateLibraryCommand
        {
            Id = libraryId,
            Title = "Other Library"
        };

        // Act
        // Assert
        (await FluentActions.Invoking(() =>
            SendAsync(command))
                .Should().ThrowAsync<ValidationException>().Where(ex => ex.Errors.ContainsKey("Title")))
                .And.Errors["Title"].Should().Contain("'Title' must be unique.");
    }

    [Test]
    public async Task ShouldUpdateLibrary()
    {
        // Arrange
        var userId = await RunAsDefaultUserAsync();
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = "/root/path"
        });
        var command = new UpdateLibraryCommand
        {
            Id = libraryId,
            Title = "Updated Library Title"
        };

        // Act
        await SendAsync(command);
        var library = await FindAsync<Library>(libraryId);

        // Assert
        library.Should().NotBeNull();
        library!.Title.Should().Be(command.Title);
        library.LastModifiedBy.Should().NotBeNull();
        library.LastModifiedBy.Should().Be(userId);
        library.LastModified.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMilliseconds(10000));
    }
}
