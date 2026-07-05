using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.ValueObjects;

namespace K7.Server.Application.Features.Diagnostics.Services;

public class OrphanIndexedFileFixBuilder(IApplicationDbContext context)
{
    public async Task<List<CreateBackgroundTasksBatchItem>> BuildCreateMediaTasksAsync(
        IReadOnlyList<Guid> indexedFileIds,
        CancellationToken cancellationToken)
    {
        if (indexedFileIds.Count == 0)
            return [];

        var seedFiles = await LoadOrphanFilesAsync(indexedFileIds, cancellationToken);
        if (seedFiles.Count == 0)
            return [];

        var expandedIds = await ExpandToRelatedOrphansAsync(seedFiles, cancellationToken);
        var files = await LoadOrphanFilesAsync(expandedIds, cancellationToken);
        if (files.Count == 0)
            return [];

        var tasks = new List<CreateBackgroundTasksBatchItem>();

        foreach (var libraryGroup in files.GroupBy(f => f.LibraryId))
        {
            var library = libraryGroup.First().Library;
            if (library is null)
                continue;

            switch (library.MediaType)
            {
                case LibraryMediaType.Movie:
                    foreach (var file in libraryGroup)
                    {
                        tasks.Add(CreateMediaTask([file.Id], MediaType.Movie, library.Id, library.MetadataProviderName));
                    }

                    break;

                case LibraryMediaType.Music:
                    foreach (var albumGroup in libraryGroup.GroupBy(f =>
                                 (f.Identification!.AlbumName ?? f.ParentDirectory, f.Identification!.ArtistName)))
                    {
                        var albumFiles = albumGroup.ToList();
                        tasks.Add(CreateMediaTask(
                            albumFiles.Select(f => f.Id).ToList(),
                            MediaType.MusicAlbum,
                            library.Id,
                            library.MetadataProviderName,
                            albumFiles[0].Id));
                    }

                    break;

                case LibraryMediaType.Serie:
                    foreach (var serieGroup in libraryGroup
                                 .Where(f => f.Identification?.SeriesTitle is not null)
                                 .GroupBy(f => f.Identification!.SeriesTitle))
                    {
                        var serieFiles = serieGroup.ToList();
                        tasks.Add(CreateMediaTask(
                            serieFiles.Select(f => f.Id).ToList(),
                            MediaType.Serie,
                            library.Id,
                            library.MetadataProviderName,
                            serieFiles[0].Id));
                    }

                    break;
            }
        }

        return tasks;
    }

    private async Task<List<Guid>> ExpandToRelatedOrphansAsync(
        List<OrphanIndexedFileRow> seedFiles,
        CancellationToken cancellationToken)
    {
        var expandedIds = seedFiles.Select(f => f.Id).ToHashSet();

        foreach (var libraryGroup in seedFiles.GroupBy(f => f.LibraryId))
        {
            var library = libraryGroup.First().Library;
            if (library is null)
                continue;

            if (library.MediaType is not (LibraryMediaType.Music or LibraryMediaType.Serie))
                continue;

            var libraryOrphans = await context.IndexedFiles
                .AsNoTracking()
                .Where(f => f.LibraryId == libraryGroup.Key && f.MediaId == null && f.Identification != null)
                .Select(f => new OrphanIndexedFileRow
                {
                    Id = f.Id,
                    LibraryId = f.LibraryId,
                    ParentDirectory = f.ParentDirectory,
                    Identification = f.Identification
                })
                .ToListAsync(cancellationToken);

            foreach (var seed in libraryGroup)
            {
                if (library.MediaType == LibraryMediaType.Music)
                {
                    var albumKey = (seed.Identification!.AlbumName ?? seed.ParentDirectory, seed.Identification.ArtistName);
                    foreach (var sibling in libraryOrphans.Where(f =>
                                 (f.Identification!.AlbumName ?? f.ParentDirectory, f.Identification.ArtistName) == albumKey))
                    {
                        expandedIds.Add(sibling.Id);
                    }
                }
                else if (seed.Identification?.SeriesTitle is { } seriesTitle)
                {
                    foreach (var sibling in libraryOrphans.Where(f => f.Identification?.SeriesTitle == seriesTitle))
                    {
                        expandedIds.Add(sibling.Id);
                    }
                }
            }
        }

        return expandedIds.ToList();
    }

    private async Task<List<OrphanIndexedFileRow>> LoadOrphanFilesAsync(
        IReadOnlyList<Guid> indexedFileIds,
        CancellationToken cancellationToken)
    {
        return await context.IndexedFiles
            .AsNoTracking()
            .Where(f => indexedFileIds.Contains(f.Id) && f.MediaId == null && f.Identification != null)
            .Select(f => new OrphanIndexedFileRow
            {
                Id = f.Id,
                LibraryId = f.LibraryId,
                ParentDirectory = f.ParentDirectory,
                Identification = f.Identification,
                Library = context.Libraries
                    .Where(l => l.Id == f.LibraryId)
                    .Select(l => new LibraryInfo
                    {
                        Id = l.Id,
                        MediaType = l.MediaType,
                        MetadataProviderName = l.MetadataProviderName
                    })
                    .FirstOrDefault()
            })
            .ToListAsync(cancellationToken);
    }

    private static CreateBackgroundTasksBatchItem CreateMediaTask(
        IReadOnlyList<Guid> indexedFileIds,
        MediaType mediaType,
        Guid libraryId,
        string metadataProviderName,
        Guid? targetEntityId = null) =>
        new()
        {
            Request = new CreateMediaCommand
            {
                IndexedFileIds = indexedFileIds.ToList(),
                MediaType = mediaType,
                LibraryId = libraryId
            },
            Priority = BackgroundTaskPriority.Normal,
            TargetEntityId = targetEntityId ?? indexedFileIds[0],
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 5,
            ConcurrencyGroup = metadataProviderName
        };

    private sealed class OrphanIndexedFileRow
    {
        public required Guid Id { get; init; }
        public required Guid LibraryId { get; init; }
        public string? ParentDirectory { get; init; }
        public MediaIdentification? Identification { get; init; }
        public LibraryInfo? Library { get; init; }
    }

    private sealed class LibraryInfo
    {
        public required Guid Id { get; init; }
        public required LibraryMediaType MediaType { get; init; }
        public required string MetadataProviderName { get; init; }
    }
}
