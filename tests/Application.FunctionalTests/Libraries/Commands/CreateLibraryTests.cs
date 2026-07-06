using K7.Server.Application.Common.Exceptions;
using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Tests.Helpers.Fixtures;

namespace K7.Server.Application.FunctionalTests.Libraries.Commands;

public class CreateLibraryTests : DatabaseFixture
{
    [Test]
    public async Task ShouldRequireMinimumFields()
    {
        // Arrange

        // Act
        var command = new CreateLibraryCommand()
        {
            MediaType = LibraryMediaType.Music,
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = null!,
            Title = null!
        };

        // Assert
        await FluentActions.Invoking(() => SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldRequireUniqueTitle()
    {
        // Arange
        await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = "/root/path"
        });
        var command = new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = "/root/path"
        };

        // Act
        // Assert
        await FluentActions.Invoking(() =>
            SendAsync(command)).Should().ThrowAsync<ValidationException>();
    }

    [Test]
    public async Task ShouldCreateLibraryWithDisabledScanSettings()
    {
        await RunAsAdministratorAsync();
        var command = new CreateLibraryCommand
        {
            Title = "Scan Settings Library",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = "/root/path",
            TriggerFileIndexingOnCreation = false,
            RealtimeMonitorEnabled = false,
            AutoScanIntervalHours = 0
        };

        var id = await SendAsync(command);
        var library = await FindAsync<Library>(id);

        library.Should().NotBeNull();
        library!.RealtimeMonitorEnabled.Should().BeFalse();
        library.AutoScanIntervalHours.Should().Be(0);
    }

    [Test]
    public async Task ShouldCreateLibrary()
    {
        // Arrange
        var userId = await RunAsAdministratorAsync();
        var command = new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Movie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            RootPath = "/root/path"
        };

        // Act
        var id = await SendAsync(command);
        var library = await FindAsync<Library>(id);

        // Assert
        library.Should().NotBeNull();
        library!.Title.Should().Be(command.Title);
        library.CreatedBy.Should().Be(userId);
        library.Created.Should().BeCloseTo(DateTime.Now, TimeSpan.FromMilliseconds(10000));
    }
}
