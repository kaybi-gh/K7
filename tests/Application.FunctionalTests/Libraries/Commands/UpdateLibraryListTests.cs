using Ardalis.GuardClauses;
using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Application.Features.Libraries.Commands.UpdateLibrary;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Tests.Helpers.Fixtures;

namespace K7.Server.Application.FunctionalTests.Libraries.Commands;

public class UpdateLibraryListTests : DatabaseFixture
{
    [Test]
    public async Task ShouldRequireValidLibraryId()
    {
        await RunAsAdministratorAsync();

        // Arrange
        var command = new UpdateLibraryCommand
        {
            Id = Guid.NewGuid(),
            Title = "New Title"
        };

        // Act
        // Assert
        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<NotFoundException>();
    }

    [Test]
    public async Task ShouldPersistDisabledScanSettings()
    {
        await RunAsAdministratorAsync();
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "Scan Settings Library",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = "/root/path",
            RealtimeMonitorEnabled = true,
            AutoScanIntervalHours = 6
        });

        await SendAsync(new UpdateLibraryCommand
        {
            Id = libraryId,
            RealtimeMonitorEnabled = false,
            AutoScanIntervalHours = 0
        });

        var library = await FindAsync<Library>(libraryId);

        library.Should().NotBeNull();
        library!.RealtimeMonitorEnabled.Should().BeFalse();
        library.AutoScanIntervalHours.Should().Be(0);
    }

    [Test]
    public async Task ShouldUpdateLibrary()
    {
        // Arrange
        var userId = await RunAsAdministratorAsync();
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
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
