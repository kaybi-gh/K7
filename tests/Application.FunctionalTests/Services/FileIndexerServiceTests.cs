using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Tests.Helpers.Fixtures;
using K7.Tests.Helpers.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.FunctionalTests.Services;

public class FileIndexerServiceTests : FileAndDatabaseFixture
{
    [Test]
    public async Task ShouldAddOneIndexedFile()
    {
        var fileIndexer = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        var library = await CreateMusicLibraryAsync();

        (await CountAsync<IndexedFile>()).Should().Be(0);

        FileHelper.CreateTestFile("test.mp3", "content");
        FileHelper.CreateTestFile("ignored.extension", "content");

        await fileIndexer.IndexAsync(library, CancellationToken.None);

        (await CountAsync<IndexedFile>()).Should().Be(1);
        var indexed = await GetIndexedFilesAsync();
        indexed.Should().ContainSingle().Which.Name.Should().Be("test");
    }

    [Test]
    public async Task ShouldRemoveOneIndexedFile()
    {
        var fileIndexer = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        FileHelper.CreateTestFile("test.mp3", "content");
        FileHelper.CreateTestFile("noise.mp3", "content");
        var library = await CreateMusicLibraryAsync();

        await fileIndexer.IndexAsync(library, CancellationToken.None);
        (await CountAsync<IndexedFile>()).Should().Be(2);

        FileHelper.DeleteTestFile("test.mp3");
        await fileIndexer.IndexAsync(library, CancellationToken.None);

        (await CountAsync<IndexedFile>()).Should().Be(1);
        var remaining = await GetIndexedFilesAsync();
        remaining.Should().ContainSingle().Which.Name.Should().Be("noise");
    }

    [Test]
    public async Task ShouldUpdateIndexedFile()
    {
        var fileIndexer = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        FileHelper.CreateTestFile("test.mp3", "content");
        var library = await CreateMusicLibraryAsync();

        await fileIndexer.IndexAsync(library, CancellationToken.None);
        var before = (await GetIndexedFilesAsync()).Should().ContainSingle().Subject;

        // Ensure LastWriteTimeUtc advances on Windows (same-second writes can be treated as unchanged).
        await Task.Delay(1100);
        FileHelper.DeleteTestFile("test.mp3");
        FileHelper.CreateTestFile("test.mp3", "content2");

        await fileIndexer.IndexAsync(library, CancellationToken.None);

        var after = (await GetIndexedFilesAsync()).Should().ContainSingle().Subject;
        after.Id.Should().Be(before.Id);
        after.Size.Should().NotBe(before.Size);
        after.LastWriteTimeUtc.Should().BeAfter(before.LastWriteTimeUtc);
    }

    [Test]
    public async Task ShouldNotUpdateIndexedFile()
    {
        var fileIndexer = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        FileHelper.CreateTestFile("test.mp3", "content");
        var library = await CreateMusicLibraryAsync();

        await fileIndexer.IndexAsync(library, CancellationToken.None);
        var before = (await GetIndexedFilesAsync()).Should().ContainSingle().Subject;

        await fileIndexer.IndexAsync(library, CancellationToken.None);

        var after = (await GetIndexedFilesAsync()).Should().ContainSingle().Subject;
        after.Id.Should().Be(before.Id);
        after.Hash.Should().Be(before.Hash);
        after.Size.Should().Be(before.Size);
        after.LastWriteTimeUtc.Should().Be(before.LastWriteTimeUtc);
    }

    private static async Task<Library> CreateMusicLibraryAsync()
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

    private static async Task<List<IndexedFile>> GetIndexedFilesAsync()
    {
        var context = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.IndexedFiles.AsNoTracking().ToListAsync();
    }
}
