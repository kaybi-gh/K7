using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Extensions;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTasksBatch;
using K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;
using K7.Server.Application.Features.Libraries.Commands.DeleteIndexedFile;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Helpers;
using K7.Server.Application.Models;
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
    private const int ScanParallelism = 4;
    private const int ProgressReportInterval = 250;

    private readonly ILogger<FileIndexer> _logger;
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;
    private readonly IAudioTagReader _audioTagReader;
    private readonly ILibraryScanProgressReporter _progressReporter;

    public FileIndexer(
        ILogger<FileIndexer> logger,
        IApplicationDbContext context,
        ISender sender,
        IAudioTagReader audioTagReader,
        ILibraryScanProgressReporter progressReporter)
    {
        _logger = logger;
        _context = context;
        _sender = sender;
        _audioTagReader = audioTagReader;
        _progressReporter = progressReporter;
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
            library.IndexedFiles = existingFiles;

            var diff = BuildDiff(library.Id, scannedEntries, existingFiles, skippedFilePaths);

            var unchangedToReIdentify = diff.UnchangedFiles
                .Where(x => x.Identification is null || !x.MediaId.HasValue)
                .ToList();

            var modifiedAsNew = diff.ModifiedFiles
                .Select(pair => ApplyContentChange(pair.ExistingFile, pair.ScannedFile))
                .ToList();

            var toBeIdentifiedFiles = diff.AddedFiles
                .Concat(unchangedToReIdentify)
                .Concat(modifiedAsNew.Where(f => f.Identification is null || !f.MediaId.HasValue))
                .ToList();

            var unchangedCount = diff.UnchangedFiles.Count;
            var addedCount = diff.AddedFiles.Count;
            var removedCount = diff.RemovedFiles.Count;
            var renamedCount = diff.RenamedFiles.Count;
            var modifiedCount = diff.ModifiedFiles.Count;

            _logger.LogInformation(
                "Found {UnchangedCount} unchanged, {AddedCount} added, {ModifiedCount} modified, {RemovedCount} removed, {SkippedCount} skipped, {RenamedCount} renamed files. {ToIdentifyCount} files to be identified. {InaccessibleCount} inaccessible directories.",
                unchangedCount, addedCount, modifiedCount, removedCount, skippedFilePaths.Count, renamedCount, toBeIdentifiedFiles.Count, inaccessiblePaths.Count);

            foreach (var (path, error) in inaccessiblePaths)
            {
                _logger.LogWarning("Inaccessible directory {Path}: {Error}", path, error);
            }

            IdentifyFiles(library, toBeIdentifiedFiles, backgroundTasks);
            ProcessAddedFiles(library, diff.AddedFiles, backgroundTasks);
            ProcessAddedFiles(library, modifiedAsNew, backgroundTasks);
            await ProcessUnchangedFilesMissingMetadataAsync(library, diff.UnchangedFiles, backgroundTasks, cancellationToken);
            await ProcessRemovedFilesAsync(diff.RemovedFiles, cancellationToken);
            ProcessRenamedFiles(library, diff.RenamedFiles, backgroundTasks);

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

            foreach (var batch in diff.AddedFiles.Chunk(SaveBatchSize))
            {
                _context.IndexedFiles.AddRange(batch);
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var file in batch)
                    _context.Entry(file).State = EntityState.Detached;
            }

            await _context.SaveChangesAsync(cancellationToken);

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
            var scopedFiles = await query.ToListAsync(cancellationToken);
            return scopedFiles
                .Where(f => scopePaths.Any(scope => PathHelper.IsPathInScope(f.Path, scope)))
                .ToList();
        }

        return await query.ToListAsync(cancellationToken);
    }

    private LibraryScanDiff BuildDiff(
        Guid libraryId,
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
            entry => entry.ToIndexedFile(libraryId, ComputeHash(entry)));
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

        ProcessAddedFiles(library, filesMissingMetadata, backgroundTasks);
    }

    private async Task ProcessRemovedFilesAsync(IReadOnlyList<IndexedFile> removedFiles, CancellationToken cancellationToken)
    {
        if (removedFiles.Count == 0)
            return;

        _logger.LogInformation("Removing {Count} indexed files no longer present on disk.", removedFiles.Count);

        foreach (var file in removedFiles)
        {
            await _sender.Send(new DeleteIndexedFileCommand(file.Id), cancellationToken);
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
}
