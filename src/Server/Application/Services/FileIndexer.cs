using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Extensions;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Helpers;
using K7.Server.Application.Models;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Entities.Metadatas.Files;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;

public class FileIndexer : IFileIndexer
{
    private const int SaveBatchSize = 500;
    private const int ScanParallelism = 4;
    private const int ProgressReportInterval = 250;

    private readonly ILogger<FileIndexer> _logger;
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly IAudioTagReader _audioTagReader;
    private readonly ILibraryScanProgressReporter _progressReporter;
    private readonly ILibraryNotifier _libraryNotifier;

    public FileIndexer(
        ILogger<FileIndexer> logger,
        IApplicationDbContext context,
        ISender sender,
        IAudioTagReader audioTagReader,
        ILibraryScanProgressReporter progressReporter,
        ILibraryNotifier libraryNotifier)
    {
        _logger = logger;
        _context = context;
        _sender = sender;
        _audioTagReader = audioTagReader;
        _progressReporter = progressReporter;
        _libraryNotifier = libraryNotifier;
    }

    public Task<LibraryScanResult> IndexAsync(Library library, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(library.RootPath);
        return IndexInternalAsync(library, ct => FileInfoHelper.GetSupportedFilesRecursively(library.RootPath!, ct), cancellationToken);
    }

    public Task<LibraryScanResult> IndexPathsAsync(Library library, IReadOnlyList<string> paths, CancellationToken cancellationToken = default)
    {
        Guard.Against.NullOrEmpty(library.RootPath);

        var normalizedRoot = PathHelper.NormalizePath(library.RootPath!);
        var normalizedPaths = paths
            .Select(path => PathHelper.NormalizePath(path, library.RootPath!))
            .Where(path => PathHelper.IsPathUnderRoot(path, normalizedRoot))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedPaths.Count == 0)
        {
            throw new ArgumentException("No paths are within the library root.", nameof(paths));
        }

        return IndexInternalAsync(
            library,
            ct => FileInfoHelper.GetSupportedFilesForPaths(normalizedPaths, ct),
            cancellationToken,
            scopePaths: normalizedPaths);
    }

    private async Task<LibraryScanResult> IndexInternalAsync(
        Library library,
        Func<CancellationToken, (List<ScannedFileEntry> Files, List<(string Path, string Error)> InaccessiblePaths)> scanFiles,
        CancellationToken cancellationToken,
        IReadOnlyList<string>? scopePaths = null)
    {
        List<CreateBackgroundTasksBatchItem> backgroundTasks = [];

        try
        {
            _logger.LogInformation("Starting indexing files of library {LibraryId}.", library.Id);

            await _progressReporter.ReportProgressAsync(library.Id, 0, 0, "scanning", cancellationToken);

            var (scannedEntries, inaccessiblePaths) = scanFiles(cancellationToken);
            var skippedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            await _progressReporter.ReportProgressAsync(library.Id, 0, scannedEntries.Count, "comparing", cancellationToken);

            var existingFiles = await LoadExistingFilesAsync(library.Id, scopePaths, cancellationToken);

            var diff = BuildDiff(library.Id, library.RootPath!, scannedEntries, existingFiles, skippedFilePaths);
            var removedFiles = CollectFilesAbsentFromDisk(diff.RemovedFiles, existingFiles);
            var removedIds = removedFiles.Select(f => f.Id).ToHashSet();

            var unchangedFiles = diff.UnchangedFiles.Where(f => !removedIds.Contains(f.Id)).ToList();
            var addedFiles = diff.AddedFiles;
            var renamedFiles = diff.RenamedFiles.Where(pair => !removedIds.Contains(pair.OldFile.Id)).ToList();

            var unchangedToReIdentify = unchangedFiles
                .Where(x => x.Identification is null || !x.MediaId.HasValue)
                .ToList();

            var modifiedAsNew = diff.ModifiedFiles
                .Where(pair => !removedIds.Contains(pair.ExistingFile.Id))
                .Select(pair => ApplyContentChange(pair.ExistingFile, pair.ScannedFile))
                .ToList();

            var toBeIdentifiedFiles = addedFiles
                .Concat(unchangedToReIdentify)
                .Concat(modifiedAsNew.Where(f => f.Identification is null || !f.MediaId.HasValue))
                .ToList();

            var unchangedCount = unchangedFiles.Count;
            var addedCount = addedFiles.Count;
            var removedCount = removedFiles.Count;
            var renamedCount = renamedFiles.Count;
            var modifiedCount = modifiedAsNew.Count;

            _logger.LogInformation(
                "Found {UnchangedCount} unchanged, {AddedCount} added, {ModifiedCount} modified, {RemovedCount} removed, {SkippedCount} skipped, {RenamedCount} renamed files. {ToIdentifyCount} files to be identified. {InaccessibleCount} inaccessible directories.",
                unchangedCount, addedCount, modifiedCount, removedCount, skippedFilePaths.Count, renamedCount, toBeIdentifiedFiles.Count, inaccessiblePaths.Count);

            foreach (var file in removedFiles)
            {
                _logger.LogInformation("Removing indexed file no longer present on disk: {Path}", file.Path);
            }

            foreach (var (path, error) in inaccessiblePaths)
            {
                _logger.LogWarning("Inaccessible directory {Path}: {Error}", path, error);
            }

            await ProcessRemovedFilesAsync(removedFiles, cancellationToken);

            IdentifyFiles(library, toBeIdentifiedFiles, backgroundTasks);
            ProcessAddedFiles(library, addedFiles, backgroundTasks);
            ProcessAddedFiles(library, modifiedAsNew, backgroundTasks);
            await ProcessUnchangedFilesMissingMetadataAsync(library, unchangedFiles, backgroundTasks, cancellationToken);
            ProcessRenamedFiles(library, renamedFiles, backgroundTasks);

            if (unchangedToReIdentify.Count > 0)
            {
                _context.IndexedFiles.UpdateRange(unchangedToReIdentify);
            }

            if (modifiedAsNew.Count > 0)
            {
                _context.IndexedFiles.UpdateRange(modifiedAsNew);
            }

            foreach (var (existingFile, scannedFile) in diff.BackfillTimestampFiles)
            {
                existingFile.LastWriteTimeUtc = scannedFile.LastWriteTimeUtc;
                _context.IndexedFiles.Update(existingFile);
            }

            foreach (var batch in addedFiles.Chunk(SaveBatchSize))
            {
                _context.IndexedFiles.AddRange(batch);
                await _context.SaveChangesAsync(cancellationToken);
                ClearChangeTracker();
            }

            await _context.SaveChangesAsync(cancellationToken);
            ClearChangeTracker();

            if (backgroundTasks.Count > 0)
            {
                await _sender.Send(new CreateBackgroundTasksBatchCommand(backgroundTasks), cancellationToken);
            }

            await _progressReporter.ReportProgressAsync(library.Id, scannedEntries.Count, scannedEntries.Count, "completed", cancellationToken);

            return new LibraryScanResult(
                unchangedCount,
                addedCount + modifiedCount,
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

    private async Task<List<IndexedFile>> LoadExistingFilesAsync(
        Guid libraryId,
        IReadOnlyList<string>? scopePaths,
        CancellationToken cancellationToken)
    {
        var query = _context.IndexedFiles
            .Where(f => f.LibraryId == libraryId)
            .AsNoTracking();

        if (scopePaths is { Count: > 0 })
        {
            IQueryable<IndexedFile>? scopedQuery = null;

            foreach (var scopePath in scopePaths.Select(PathHelper.NormalizePath))
            {
                var exactPath = scopePath;
                var forwardPrefix = scopePath.TrimEnd('/', '\\') + '/';
                var backPrefix = scopePath.TrimEnd('/', '\\') + '\\';
                var branch = query.Where(f =>
                    f.Path == exactPath
                    || f.Path.StartsWith(forwardPrefix)
                    || f.Path.StartsWith(backPrefix));

                scopedQuery = scopedQuery is null ? branch : scopedQuery.Union(branch);
            }

            return await scopedQuery!.ToListAsync(cancellationToken);
        }

        return await query.ToListAsync(cancellationToken);
    }

    private LibraryScanDiff BuildDiff(
        Guid libraryId,
        string libraryRootPath,
        IReadOnlyList<ScannedFileEntry> scannedEntries,
        IReadOnlyList<IndexedFile> existingFiles,
        HashSet<string> skippedFilePaths)
    {
        var hashCache = new ConcurrentDictionary<string, uint>(StringComparer.OrdinalIgnoreCase);

        uint ComputeHash(ScannedFileEntry entry)
        {
            return hashCache.GetOrAdd(entry.Path, _ => new FileInfo(entry.Path).ComputeFileHash());
        }

        return LibraryScanDiffBuilder.Build(
            scannedEntries,
            existingFiles,
            skippedFilePaths,
            entry => entry.ToIndexedFile(libraryId, ComputeHash(entry)),
            libraryRootPath);
    }

    private static List<IndexedFile> CollectFilesAbsentFromDisk(
        IReadOnlyList<IndexedFile> removedFromDiff,
        IReadOnlyList<IndexedFile> existingFiles)
    {
        var removedById = removedFromDiff.ToDictionary(f => f.Id);

        foreach (var existing in existingFiles)
        {
            if (removedById.ContainsKey(existing.Id))
                continue;

            if (File.Exists(existing.Path))
                continue;

            removedById[existing.Id] = existing;
        }

        return removedById.Values.ToList();
    }

    private static IndexedFile ApplyContentChange(IndexedFile existingFile, ScannedFileEntry scannedFile)
    {
        var fileInfo = new FileInfo(scannedFile.Path);
        existingFile.Hash = fileInfo.ComputeFileHash();
        existingFile.Size = scannedFile.Size;
        existingFile.LastWriteTimeUtc = scannedFile.LastWriteTimeUtc;
        existingFile.Name = scannedFile.Name;
        existingFile.Extension = scannedFile.Extension;
        existingFile.ParentDirectory = scannedFile.ParentDirectory;
        return existingFile;
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
                    new ParallelOptions { MaxDegreeOfParallelism = ScanParallelism },
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

        foreach (var file in addedFiles.Where(f => File.Exists(f.Path)))
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

        var filesMissingMetadata = unchangedFiles
            .Where(f => File.Exists(f.Path))
            .Where(f => !idsWithMetadata.Contains(f.Id))
            .ToList();
        if (filesMissingMetadata.Count == 0) return;

        _logger.LogInformation("Found {Count} unchanged files missing FileMetadata, creating tasks.", filesMissingMetadata.Count);

        ProcessAddedFiles(library, filesMissingMetadata, backgroundTasks);
    }

    private async Task ProcessRemovedFilesAsync(IReadOnlyList<IndexedFile> removedFiles, CancellationToken cancellationToken)
    {
        if (removedFiles.Count == 0)
            return;

        _logger.LogInformation("Removing {Count} indexed files no longer present on disk.", removedFiles.Count);

        var notifications = new HashSet<(Guid MediaId, Guid LibraryId)>();

        foreach (var batchIds in removedFiles.Select(f => f.Id).Chunk(SaveBatchSize))
        {
            var batch = await _context.IndexedFiles
                .Include(x => x.FileMetadata)
                .Where(x => batchIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            foreach (var entity in batch)
            {
                var formerMediaId = entity.MediaId;
                entity.MediaId = null;

                if (entity.FileMetadata is VideoFileMetadata videoMetadata)
                {
                    if (videoMetadata.Thumbnails is not null)
                    {
                        _context.MetadataPictures.Remove(videoMetadata.Thumbnails);
                        videoMetadata.Thumbnails = null;
                    }

                    _context.FileMetadatas.Remove(entity.FileMetadata);
                    entity.FileMetadata = null;
                }
                else if (entity.FileMetadata is not null)
                {
                    _context.FileMetadatas.Remove(entity.FileMetadata);
                    entity.FileMetadata = null;
                }

                _context.IndexedFiles.Remove(entity);
                entity.AddDomainEvent(new IndexedFileDeletedEvent(entity, formerMediaId, entity.LibraryId));

                if (formerMediaId is Guid mediaId)
                    notifications.Add((mediaId, entity.LibraryId));
            }

            await _context.SaveChangesAsync(cancellationToken);
            ClearChangeTracker();
        }

        foreach (var (mediaId, libraryId) in notifications)
        {
            await _libraryNotifier.NotifyMediaIndexedFilesUpdatedAsync(mediaId, libraryId, cancellationToken);
        }
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
            oldFile.LastWriteTimeUtc = newFile.LastWriteTimeUtc;

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

    private void ClearChangeTracker()
    {
        if (_context is DbContext dbContext)
            dbContext.ChangeTracker.Clear();
    }
}
