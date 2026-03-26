using K7.Server.Application.Features.Libraries.Commands.CreateLibrary;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Tests.Helpers.Fixtures;
using K7.Tests.Helpers.Helpers;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.FunctionalTests.Services;

public class FileIndexerServiceTests : FileAndDatabaseFixture
{
    [Test]
    public async Task ShouldAddOneIndexedFile()
    {
        // Arrange
        var fileIndexerService = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Music,
            RootPath = FileHelper.TestDirectoryPath,
            TriggerFileIndexingOnCreation = false
        });
        var library = await FindAsync<Library>(libraryId);
        library!.IndexedFiles!.Count().Should().Be(0);

        // Act
        FileHelper.CreateTestFile("test.mp3", "content");
        FileHelper.CreateTestFile("ignored.extension", "content");
        CancellationTokenSource cts = new();
        await fileIndexerService.IndexAsync(library!, cts.Token);

        // Assert
        library!.IndexedFiles!.Count().Should().Be(1);
    }

    [Test]
    public async Task ShouldRemoveOneIndexedFile()
    {
        // Arrange
        var fileIndexerService = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        FileHelper.CreateTestFile("test.mp3", "content");
        FileHelper.CreateTestFile("noise.mp3", "content");
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Music,
            RootPath = FileHelper.TestDirectoryPath,
            TriggerFileIndexingOnCreation = true
        });
        var library = await FindAsync<Library>(libraryId);
        library!.IndexedFiles!.Count().Should().Be(2);

        // Act
        FileHelper.DeleteTestFile("test.mp3");
        CancellationTokenSource cts = new();
        await fileIndexerService.IndexAsync(library!, cts.Token);

        // Assert
        library!.IndexedFiles!.Count().Should().Be(1);
    }

    [Test]
    public async Task ShouldUpdateIndexedFile()
    {
        // Arrange
        var fileIndexerService = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        FileHelper.CreateTestFile("test.mp3", "content");
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Music,
            RootPath = FileHelper.TestDirectoryPath,
            TriggerFileIndexingOnCreation = true
        });
        var library = await FindAsync<Library>(libraryId);
        library!.IndexedFiles!.Count().Should().Be(1);
        CancellationTokenSource cts = new();
        var lastModifiedIndexedFile = library.IndexedFiles!.First().LastModified;

        // Act
        FileHelper.DeleteTestFile("test.mp3");
        FileHelper.CreateTestFile("test.mp3", "content2");
        await fileIndexerService.IndexAsync(library!, cts.Token);

        // Assert
        library!.IndexedFiles!.Count().Should().Be(1);
        lastModifiedIndexedFile.Should().BeBefore(library.IndexedFiles!.First().LastModified);
    }

    [Test]
    public async Task ShouldNotUpdateIndexedFile()
    {
        // Arrange
        var fileIndexerService = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        FileHelper.CreateTestFile("test.mp3", "content");
        var libraryId = await SendAsync(new CreateLibraryCommand
        {
            Title = "New Library",
            MediaType = LibraryMediaType.Music,
            RootPath = FileHelper.TestDirectoryPath,
            TriggerFileIndexingOnCreation = true
        });
        var library = await FindAsync<Library>(libraryId);
        library!.IndexedFiles!.Count().Should().Be(1);
        CancellationTokenSource cts = new();
        var lastModifiedIndexedFile = library.IndexedFiles!.First().LastModified;

        // Act
        await fileIndexerService.IndexAsync(library!, cts.Token);

        // Assert
        library!.IndexedFiles!.Count().Should().Be(1);
        lastModifiedIndexedFile.Should().BeBefore(library.IndexedFiles!.First().LastModified);
    }
}
