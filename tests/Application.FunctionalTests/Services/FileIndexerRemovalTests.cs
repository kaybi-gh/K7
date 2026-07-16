using K7.Server.Application.Features.Libraries.Commands.IndexLibraryFiles;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Tests.Helpers.Fixtures;
using K7.Tests.Helpers.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.FunctionalTests.Services;

public class FileIndexerRemovalTests : FileAndDatabaseFixture
{
    [Test]
    public async Task IndexAsync_ShouldRemoveIndexedFile_WhenPhysicalFileIsDeleted()
    {
        var fileIndexer = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        FileHelper.CreateTestFile("track-a.flac", "content-a");
        FileHelper.CreateTestFile("track-b.flac", "content-b");

        var library = await CreateMusicLibraryAsync();

        await fileIndexer.IndexAsync(library, CancellationToken.None);
        (await CountAsync<IndexedFile>()).Should().Be(2);

        FileHelper.DeleteTestFile("track-a.flac");

        await fileIndexer.IndexAsync(library, CancellationToken.None);

        (await CountAsync<IndexedFile>()).Should().Be(1);
        var remaining = await GetIndexedFilesAsync();
        remaining.Should().ContainSingle().Which.Name.Should().Be("track-b");
    }

    [Test]
    public async Task IndexLibraryFilesCommand_ShouldRemoveIndexedFile_WhenPhysicalFileIsDeleted()
    {
        FileHelper.CreateTestFile("track-a.flac", "content-a");
        FileHelper.CreateTestFile("track-b.flac", "content-b");

        var library = await CreateMusicLibraryAsync();
        await SendAsync(new IndexLibraryFilesCommand(library.Id));
        (await CountAsync<IndexedFile>()).Should().Be(2);

        FileHelper.DeleteTestFile("track-a.flac");

        await SendAsync(new IndexLibraryFilesCommand(library.Id));

        (await CountAsync<IndexedFile>()).Should().Be(1);
        var remaining = await GetIndexedFilesAsync();
        remaining.Should().ContainSingle().Which.Name.Should().Be("track-b");
    }

    private async Task<Library> CreateMusicLibraryAsync()
    {
        var group = new LibraryGroup
        {
            Id = Guid.NewGuid(),
            Title = "Music Group",
            MediaType = LibraryMediaType.Music
        };
        await AddAsync(group);

        var library = new Library
        {
            Id = Guid.NewGuid(),
            Title = "Music Library",
            MediaType = LibraryMediaType.Music,
            RootPath = FileHelper.TestDirectoryPath,
            MetadataProviderName = "musicbrainz",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            LibraryGroupId = group.Id,
            RealtimeMonitorEnabled = false
        };
        await AddAsync(library);
        return library;
    }

    private async Task<List<IndexedFile>> GetIndexedFilesAsync()
    {
        var context = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.IndexedFiles.AsNoTracking().ToListAsync();
    }
}
