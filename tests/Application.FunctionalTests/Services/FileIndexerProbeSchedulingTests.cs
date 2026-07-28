using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Infrastructure.Database.Context.Data;
using K7.Tests.Helpers.Fixtures;
using K7.Tests.Helpers.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Server.Application.FunctionalTests.Services;

/// <summary>
/// Covers the scheduling contract that makes a newly indexed media playable as soon as it is visible:
/// probes go to their own lane and are enqueued before the media creation batch.
/// </summary>
public class FileIndexerProbeSchedulingTests : FileAndDatabaseFixture
{
    [Test]
    public async Task IndexAsync_ShouldEnqueueProbeInProbeLane_NotInTranscodingLane()
    {
        var fileIndexer = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        var library = await CreateMovieLibraryAsync();
        FileHelper.CreateTestFile("Inception (2010).mkv", "content");

        await fileIndexer.IndexAsync(library, CancellationToken.None);

        var probeTasks = await GetTasksAsync(nameof(CreateFileMetadatasCommand));
        probeTasks.Should().ContainSingle()
            .Which.Lane.Should().Be(BackgroundTaskLane.Probe);
    }

    [Test]
    public async Task IndexAsync_ShouldEnqueueProbesBeforeMediaCreation()
    {
        var fileIndexer = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        var library = await CreateMovieLibraryAsync();
        FileHelper.CreateTestFile("Inception (2010).mkv", "content");

        await fileIndexer.IndexAsync(library, CancellationToken.None);

        var probeTask = (await GetTasksAsync(nameof(CreateFileMetadatasCommand))).Should().ContainSingle().Subject;
        var createMediaTask = (await GetTasksAsync(nameof(CreateMediaCommand))).Should().ContainSingle().Subject;

        // Probes are flushed with their save batch, media creation only once identification grouped
        // every file of the library.
        probeTask.Created.Should().BeOnOrBefore(createMediaTask.Created);
    }

    [Test]
    public async Task IndexAsync_ShouldClassifyProbeAndMediaCreationOnCriticalPath()
    {
        var fileIndexer = Scope.ServiceProvider.GetRequiredService<IFileIndexer>();
        var library = await CreateMovieLibraryAsync();
        FileHelper.CreateTestFile("Inception (2010).mkv", "content");

        await fileIndexer.IndexAsync(library, CancellationToken.None);

        var probeTask = (await GetTasksAsync(nameof(CreateFileMetadatasCommand))).Should().ContainSingle().Subject;
        var createMediaTask = (await GetTasksAsync(nameof(CreateMediaCommand))).Should().ContainSingle().Subject;

        probeTask.WorkClass.Should().Be(BackgroundTaskWorkClass.CriticalProbe);
        createMediaTask.WorkClass.Should().Be(BackgroundTaskWorkClass.CriticalLink);
    }

    private static async Task<List<BackgroundTask>> GetTasksAsync(string name)
    {
        var context = Scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await context.BackgroundTasks
            .AsNoTracking()
            .Where(t => t.Name == name)
            .ToListAsync();
    }

    private static async Task<Library> CreateMovieLibraryAsync()
    {
        var group = new LibraryGroup
        {
            Id = Guid.NewGuid(),
            Title = "Movie Group",
            MediaType = LibraryMediaType.Movie
        };
        await AddAsync(group);

        var library = new Library
        {
            Id = Guid.NewGuid(),
            Title = "Movie Library",
            MediaType = LibraryMediaType.Movie,
            RootPath = FileHelper.TestDirectoryPath,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            LibraryGroupId = group.Id,
            RealtimeMonitorEnabled = false
        };
        await AddAsync(library);
        return library;
    }
}
