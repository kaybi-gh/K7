using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Extensions;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.Libraries.Commands.DeleteIndexedFile;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;
public class FileIndexer : IFileIndexer
{
    private const int SaveBatchSize = 500;

    private readonly ILogger<FileIndexer> _logger;
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly IAudioTagReader _audioTagReader;

    public FileIndexer(ILogger<FileIndexer> logger, IApplicationDbContext context, ISender sender, IAudioTagReader audioTagReader)
    {
        _logger = logger;
        _context = context;
        _sender = sender;
        _audioTagReader = audioTagReader;
    }

    public async Task<LibraryScanResult> IndexAsync(Library library, CancellationToken cancellationToken)
    {
        List<CreateBackgroundTasksBatchItem> backgroundTasks = [];

        try
        {
            _logger.LogInformation("Starting indexing files of library {LibraryId}.", library.Id);

            var (indexedFiles, skippedFilePaths, inaccessiblePaths) = ScanFiles(library, cancellationToken);
            var (unchangedFiles, addedFiles, removedFiles, renamedFiles) = library.IndexedFiles.CompareTo(indexedFiles, skippedFilePaths);
            var unchangedToReIdentify = unchangedFiles.Where(x => x.Identification is null || !x.MediaId.HasValue).ToList();
            var toBeIdentifiedFiles = addedFiles.Concat(unchangedToReIdentify).ToList();

            var unchangedCount = unchangedFiles.Count();
            var addedCount = addedFiles.Count();
            var removedCount = removedFiles.Count();
            var renamedCount = renamedFiles.Count();

            _logger.LogInformation("Found {UnchangedCount} unchanged, {AddedCount} added, {RemovedCount} removed, {SkippedCount} skipped, {RenamedCount} renamed files. {ToIdentifyCount} files to be identified. {InaccessibleCount} inaccessible directories.",
                unchangedCount, addedCount, removedCount, skippedFilePaths.Count, renamedCount, toBeIdentifiedFiles.Count, inaccessiblePaths.Count);

            foreach (var (path, error) in inaccessiblePaths)
            {
                _logger.LogWarning("Inaccessible directory {Path}: {Error}", path, error);
            }

            IdentifyFiles(library, toBeIdentifiedFiles, backgroundTasks);
            ProcessAddedFiles(library, addedFiles, backgroundTasks);
            await ProcessUnchangedFilesMissingMetadataAsync(library, unchangedFiles, backgroundTasks, cancellationToken);
            ProcessRemovedFiles(removedFiles, backgroundTasks);
            ProcessRenamedFiles(library, renamedFiles, backgroundTasks);

            // Attach unchanged files that were re-identified (loaded as no-tracking)
            if (unchangedToReIdentify.Count > 0)
            {
                _context.IndexedFiles.UpdateRange(unchangedToReIdentify);
            }

            // Save added files in batches to avoid a single massive transaction
            foreach (var batch in addedFiles.Chunk(SaveBatchSize))
            {
                _context.IndexedFiles.AddRange(batch);
                await _context.SaveChangesAsync(cancellationToken);

                // Detach saved entities to keep the ChangeTracker lean
                foreach (var file in batch)
                    _context.Entry(file).State = EntityState.Detached;
            }

            // Save re-identified and other tracked changes
            await _context.SaveChangesAsync(cancellationToken);

            if (backgroundTasks.Count > 0)
            {
                await _sender.Send(new CreateBackgroundTasksBatchCommand(backgroundTasks), cancellationToken);
            }

            return new LibraryScanResult(
                unchangedCount,
                addedCount,
                removedCount,
                renamedCount,
                skippedFilePaths.Count,
                [.. skippedFilePaths],
                inaccessiblePaths);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing files in directory {RootPath}.", library.RootPath);
            throw;
        }
    }

    private (List<IndexedFile> IndexedFiles, HashSet<string> SkippedFilePaths, IReadOnlyList<(string Path, string Error)> InaccessiblePaths) ScanFiles(Library library, CancellationToken cancellationToken)
    {
        var (fileInfos, inaccessiblePaths) = FileInfoHelper.GetAllFileInfosRecursively(library.RootPath!, cancellationToken);
        ConcurrentBag<IndexedFile> indexedFiles = [];
        ConcurrentBag<string> skippedFilePaths = [];

        Parallel.ForEach(fileInfos, new ParallelOptions { CancellationToken = cancellationToken }, fileInfo =>
        {
            try
            {
                var indexedFile = fileInfo.ToIndexedFile(library.Id);
                if (indexedFile != null)
                {
                    indexedFiles.Add(indexedFile);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to index file {FilePath}, skipping.", fileInfo.FullName);
                skippedFilePaths.Add(fileInfo.FullName);
            }
        });

        return ([.. indexedFiles], [.. skippedFilePaths], inaccessiblePaths);
    }

    private void IdentifyFiles(Library library, List<IndexedFile> toBeIdentifiedFiles, List<CreateBackgroundTasksBatchItem> backgroundTasks)
    {
        if (toBeIdentifiedFiles.Count == 0) return;

        switch (library.MediaType)
        {
            case LibraryMediaType.Movie:
                foreach (var file in toBeIdentifiedFiles)
                {
                    if (file.TryIdentifyMovie(out MediaIdentification? movieIdentification))
                    {
                        file.Identification = movieIdentification;
                        backgroundTasks.Add(new CreateBackgroundTasksBatchItem()
                        {
                            Request = new CreateMediaCommand()
                            {
                                IndexedFileIds = [file.Id],
                                MediaType = MediaType.Movie,
                                LibraryId = library.Id
                            },
                            Priority = BackgroundTaskPriority.Normal,
                            TargetEntityId = file.Id,
                            TargetEntityTypeName = nameof(BaseMedia),
                            MaxAttempts = 5,
                            ConcurrencyGroup = library.MetadataProviderName
                        });
                    }
                }
                break;

            case LibraryMediaType.Music:
                foreach (var group in toBeIdentifiedFiles.GroupBy(f => f.ParentDirectory))
                {
                    var filesInSameDirectory = group.ToList();
                    foreach (var file in filesInSameDirectory)
                    {
                        file.TryIdentifyMusicTrack(library, filesInSameDirectory);
                    }
                }

                Parallel.ForEach(toBeIdentifiedFiles.Where(f => f.Identification is not null),
                    new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                    file => EnrichMusicIdentificationFromTags(file));

                foreach (var albumGroup in toBeIdentifiedFiles
                    .Where(f => f.Identification is not null)
                    .GroupBy(f => (f.Identification!.AlbumName ?? f.ParentDirectory, f.Identification.ArtistName)))
                {
                    var albumFiles = albumGroup.ToList();
                    backgroundTasks.Add(new CreateBackgroundTasksBatchItem()
                    {
                        Request = new CreateMediaCommand()
                        {
                            IndexedFileIds = albumFiles.Select(f => f.Id).ToList(),
                            MediaType = MediaType.MusicAlbum,
                            LibraryId = library.Id
                        },
                        Priority = BackgroundTaskPriority.Normal,
                        TargetEntityId = albumFiles[0].Id,
                        TargetEntityTypeName = nameof(BaseMedia),
                        MaxAttempts = 5,
                        ConcurrencyGroup = library.MetadataProviderName
                    });
                }
                break;

            case LibraryMediaType.Serie:
                foreach (var group in toBeIdentifiedFiles.GroupBy(f => f.ParentDirectory))
                {
                    var filesInSameDirectory = group.ToList();
                    foreach (var file in filesInSameDirectory)
                        file.TryIdentifySerieEpisode(library, filesInSameDirectory);
                }

                foreach (var serieGroup in toBeIdentifiedFiles
                    .Where(f => f.Identification?.SeriesTitle is not null)
                    .GroupBy(f => f.Identification!.SeriesTitle))
                {
                    var serieFiles = serieGroup.ToList();
                    backgroundTasks.Add(new CreateBackgroundTasksBatchItem()
                    {
                        Request = new CreateMediaCommand()
                        {
                            IndexedFileIds = serieFiles.Select(f => f.Id).ToList(),
                            MediaType = MediaType.Serie,
                            LibraryId = library.Id
                        },
                        Priority = BackgroundTaskPriority.Normal,
                        TargetEntityId = serieFiles[0].Id,
                        TargetEntityTypeName = nameof(BaseMedia),
                        MaxAttempts = 5,
                        ConcurrencyGroup = library.MetadataProviderName
                    });
                }
                break;
        }
    }

    private static void ProcessAddedFiles(Library library, IEnumerable<IndexedFile> addedFiles, List<CreateBackgroundTasksBatchItem> backgroundTasks)
    {
        var fileType = library.MediaType switch
        {
            LibraryMediaType.Movie => FileType.Video,
            LibraryMediaType.Music => FileType.Audio,
            LibraryMediaType.Serie => FileType.Video,
            _ => throw new InvalidOperationException(),
        };

        foreach (var file in addedFiles)
        {
            backgroundTasks.Add(new CreateBackgroundTasksBatchItem()
            {
                Request = new CreateFileMetadatasCommand()
                {
                    Id = file.Id,
                    FileType = fileType
                },
                Priority = BackgroundTaskPriority.VeryHigh,
                TargetEntityId = file.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 5,
                ConcurrencyGroup = "ffmpeg"
            });
        }
    }

    private async Task ProcessUnchangedFilesMissingMetadataAsync(Library library, IEnumerable<IndexedFile> unchangedFiles, List<CreateBackgroundTasksBatchItem> backgroundTasks, CancellationToken cancellationToken)
    {
        var unchangedIds = unchangedFiles.Select(f => f.Id).ToList();
        if (unchangedIds.Count == 0) return;

        var idsWithMetadata = (await _context.FileMetadatas
            .Where(fm => unchangedIds.Contains(fm.IndexedFileId))
            .Select(fm => fm.IndexedFileId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var filesMissingMetadata = unchangedFiles.Where(f => !idsWithMetadata.Contains(f.Id)).ToList();
        if (filesMissingMetadata.Count == 0) return;

        _logger.LogInformation("Found {Count} unchanged files missing FileMetadata, creating tasks.", filesMissingMetadata.Count);

        var fileType = library.MediaType switch
        {
            LibraryMediaType.Movie => FileType.Video,
            LibraryMediaType.Music => FileType.Audio,
            LibraryMediaType.Serie => FileType.Video,
            _ => throw new InvalidOperationException(),
        };

        foreach (var file in filesMissingMetadata)
        {
            backgroundTasks.Add(new CreateBackgroundTasksBatchItem()
            {
                Request = new CreateFileMetadatasCommand()
                {
                    Id = file.Id,
                    FileType = fileType
                },
                Priority = BackgroundTaskPriority.VeryHigh,
                TargetEntityId = file.Id,
                TargetEntityTypeName = nameof(IndexedFile),
                MaxAttempts = 5,
                ConcurrencyGroup = "ffmpeg"
            });
        }
    }

    private static void ProcessRemovedFiles(IEnumerable<IndexedFile> removedFiles, List<CreateBackgroundTasksBatchItem> backgroundTasks)
    {
        backgroundTasks.AddRange(removedFiles.Select(x => new CreateBackgroundTasksBatchItem()
        {
            Request = new DeleteIndexedFileCommand(x.Id),
            Priority = BackgroundTaskPriority.Normal,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 5
        }));
    }

    private void ProcessRenamedFiles(Library library, IEnumerable<(IndexedFile NewFile, IndexedFile OldFile)> renamedFiles, List<CreateBackgroundTasksBatchItem> backgroundTasks)
    {
        foreach (var (newFile, oldFile) in renamedFiles)
        {
            oldFile.Extension = newFile.Extension;
            oldFile.Hash = newFile.Hash;
            oldFile.Name = newFile.Name;
            oldFile.ParentDirectory = newFile.ParentDirectory;
            oldFile.Path = newFile.Path;
            oldFile.Size = newFile.Size;

            if (library.MediaType == LibraryMediaType.Movie
                && newFile.TryIdentifyMovie(out MediaIdentification? movieIdentification)
                && movieIdentification != oldFile.Identification)
            {
                oldFile.Identification = movieIdentification;
                backgroundTasks.Add(new CreateBackgroundTasksBatchItem()
                {
                    Request = new CreateMediaCommand()
                    {
                        IndexedFileIds = [oldFile.Id],
                        MediaType = MediaType.Movie,
                        LibraryId = library.Id
                    },
                    Priority = BackgroundTaskPriority.Normal,
                    TargetEntityId = oldFile.Id,
                    TargetEntityTypeName = nameof(BaseMedia),
                    MaxAttempts = 5,
                    ConcurrencyGroup = library.MetadataProviderName
                });
            }

            _context.IndexedFiles.Update(oldFile);
        }
    }

    private void EnrichMusicIdentificationFromTags(IndexedFile file)
    {
        if (file.Identification is null) return;

        try
        {
            var tags = _audioTagReader.ReadTags(file.Path, includeCoverArt: false);
            if (tags is null) return;

            if (!string.IsNullOrEmpty(tags.Album))
                file.Identification.AlbumName = tags.Album;

            var artist = tags.AlbumArtists.FirstOrDefault() ?? tags.Artists.FirstOrDefault();
            if (!string.IsNullOrEmpty(artist))
                file.Identification.ArtistName = artist;

            if (!string.IsNullOrEmpty(tags.Title))
                file.Identification.Title = tags.Title;

            if (tags.TrackNumber.HasValue)
                file.Identification.TrackNumber = tags.TrackNumber;

            if (tags.Year.HasValue)
                file.Identification.ReleaseYear = new DateOnly(tags.Year.Value, 1, 1);
        }
        catch
        {
            // Tag reading failure is non-fatal - directory-based identification remains
        }
    }
}
