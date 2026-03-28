using System.Collections.Concurrent;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Extensions;
using K7.Server.Application.Features.BackgroundTasks.Commands.CreateBackgroundTask;
using K7.Server.Application.Features.Libraries.Commands.DeleteIndexedFile;
using K7.Server.Application.Features.Medias.Commands.CreateMedia;
using K7.Server.Application.Helpers;
using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Medias;
using K7.Server.Domain.Enums;
using K7.Server.Domain.Events;
using K7.Server.Domain.Interfaces;
using K7.Server.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace K7.Server.Application.Services;
public class FileIndexer : IFileIndexer
{
    private readonly ILogger<FileIndexer> _logger;
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public FileIndexer(ILogger<FileIndexer> logger, IApplicationDbContext context, ISender sender)
    {
        _logger = logger;
        _context = context;
        _sender = sender;
    }

    public async Task IndexAsync(Library library, CancellationToken cancellationToken)
    {
        List<IBaseRequest> backgroundTasks = [];

        try
        {
            _logger.LogInformation("Starting indexing files of library {LibraryId}.", library.Id);

            var (indexedFiles, skippedFilePaths) = ScanFiles(library);
            var (unchangedFiles, addedFiles, removedFiles, renamedFiles) = library.IndexedFiles.CompareTo(indexedFiles, skippedFilePaths);
            var toBeIdentifiedFiles = addedFiles.Concat(unchangedFiles.Where(x => x.Identification == null || !x.MediaId.HasValue)).ToList();

            _logger.LogInformation("Found {UnchangedCount} unchanged, {AddedCount} added, {RemovedCount} removed, {SkippedCount} skipped, {RenamedCount} renamed files. {ToIdentifyCount} files to be identified.",
                unchangedFiles.Count(), addedFiles.Count(), removedFiles.Count(), skippedFilePaths.Count, renamedFiles.Count(), toBeIdentifiedFiles.Count);

            IdentifyFiles(library, toBeIdentifiedFiles, backgroundTasks);
            ProcessAddedFiles(library, addedFiles);
            ProcessRemovedFiles(removedFiles, backgroundTasks);
            ProcessRenamedFiles(library, renamedFiles, backgroundTasks);

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var request in backgroundTasks)
            {
                await _sender.Send(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error indexing files in directory {RootPath}.", library.RootPath);
            throw;
        }
    }

    private (List<IndexedFile> IndexedFiles, HashSet<string> SkippedFilePaths) ScanFiles(Library library)
    {
        var fileInfos = FileInfoHelper.GetAllFileInfosRecursively(library.RootPath);
        ConcurrentBag<IndexedFile> indexedFiles = [];
        ConcurrentBag<string> skippedFilePaths = [];

        Parallel.ForEach(fileInfos, fileInfo =>
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

        return ([.. indexedFiles], [.. skippedFilePaths]);
    }

    private static void IdentifyFiles(Library library, List<IndexedFile> toBeIdentifiedFiles, List<IBaseRequest> backgroundTasks)
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
                        backgroundTasks.Add(new CreateBackgroundTaskCommand()
                        {
                            Request = new CreateMediaCommand()
                            {
                                IndexedFileId = file.Id,
                                MediaType = MediaType.Movie
                            },
                            Priority = BackgroundTaskPriority.Normal,
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
                        if (file.TryIdentifyMusicTrack(library, filesInSameDirectory))
                        {
                            file.Identification = file.Identification;
                            backgroundTasks.Add(new CreateBackgroundTaskCommand()
                            {
                                Request = new CreateMediaCommand()
                                {
                                    IndexedFileId = file.Id,
                                    MediaType = MediaType.MusicTrack
                                },
                                Priority = BackgroundTaskPriority.Normal,
                                TargetEntityTypeName = nameof(BaseMedia),
                                MaxAttempts = 5,
                                ConcurrencyGroup = library.MetadataProviderName
                            });
                        }
                    }
                }
                break;

            case LibraryMediaType.Serie:
                foreach (var group in toBeIdentifiedFiles.GroupBy(f => f.ParentDirectory))
                {
                    //file.TryIdentifySerieEpisode(library);
                }
                break;
        }
    }

    private void ProcessAddedFiles(Library library, IEnumerable<IndexedFile> addedFiles)
    {
        _context.IndexedFiles.AddRange(addedFiles);
        foreach (var file in addedFiles)
        {
            file.AddDomainEvent(new IndexedFileCreatedEvent(file, library.MediaType switch
            {
                LibraryMediaType.Movie => FileType.Video,
                LibraryMediaType.Music => FileType.Audio,
                LibraryMediaType.Serie => FileType.Video,
                _ => throw new InvalidOperationException(),
            }));
        }
    }

    private static void ProcessRemovedFiles(IEnumerable<IndexedFile> removedFiles, List<IBaseRequest> backgroundTasks)
    {
        backgroundTasks.AddRange(removedFiles.Select(x => new CreateBackgroundTaskCommand()
        {
            Request = new DeleteIndexedFileCommand(x.Id),
            Priority = BackgroundTaskPriority.Normal,
            TargetEntityTypeName = nameof(BaseMedia),
            MaxAttempts = 5
        }));
    }

    private void ProcessRenamedFiles(Library library, IEnumerable<(IndexedFile NewFile, IndexedFile OldFile)> renamedFiles, List<IBaseRequest> backgroundTasks)
    {
        foreach (var (newFile, oldFile) in renamedFiles)
        {
            oldFile.Extension = newFile.Extension;
            oldFile.Hash = newFile.Hash;
            oldFile.IsComposite = newFile.IsComposite;
            oldFile.IsSplitPart = newFile.IsSplitPart;
            oldFile.Name = newFile.Name;
            oldFile.ParentDirectory = newFile.ParentDirectory;
            oldFile.Path = newFile.Path;
            oldFile.Size = newFile.Size;

            if (library.MediaType == LibraryMediaType.Movie
                && newFile.TryIdentifyMovie(out MediaIdentification? movieIdentification)
                && movieIdentification != oldFile.Identification)
            {
                oldFile.Identification = movieIdentification;
                backgroundTasks.Add(new CreateBackgroundTaskCommand()
                {
                    Request = new CreateMediaCommand()
                    {
                        IndexedFileId = oldFile.Id,
                        MediaType = MediaType.Movie
                    },
                    Priority = BackgroundTaskPriority.Normal,
                    TargetEntityTypeName = nameof(BaseMedia),
                    MaxAttempts = 5,
                    ConcurrencyGroup = library.MetadataProviderName
                });
            }

            _context.IndexedFiles.Update(oldFile);
        }
    }
}
