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
        List<IBaseRequest> backgroundTasks = [];

        try
        {
            _logger.LogInformation("Starting indexing files of library {LibraryId}.", library.Id);

            var (indexedFiles, skippedFilePaths) = ScanFiles(library);
            var (unchangedFiles, addedFiles, removedFiles, renamedFiles) = library.IndexedFiles.CompareTo(indexedFiles, skippedFilePaths);
            var toBeIdentifiedFiles = addedFiles.Concat(unchangedFiles.Where(x => x.Identification == null || !x.MediaId.HasValue)).ToList();

            var unchangedCount = unchangedFiles.Count();
            var addedCount = addedFiles.Count();
            var removedCount = removedFiles.Count();
            var renamedCount = renamedFiles.Count();

            _logger.LogInformation("Found {UnchangedCount} unchanged, {AddedCount} added, {RemovedCount} removed, {SkippedCount} skipped, {RenamedCount} renamed files. {ToIdentifyCount} files to be identified.",
                unchangedCount, addedCount, removedCount, skippedFilePaths.Count, renamedCount, toBeIdentifiedFiles.Count);

            IdentifyFiles(library, toBeIdentifiedFiles, backgroundTasks);
            ProcessAddedFiles(library, addedFiles);
            ProcessRemovedFiles(removedFiles, backgroundTasks);
            ProcessRenamedFiles(library, renamedFiles, backgroundTasks);

            await _context.SaveChangesAsync(cancellationToken);

            foreach (var request in backgroundTasks)
            {
                await _sender.Send(request, cancellationToken);
            }

            return new LibraryScanResult(
                unchangedCount,
                addedCount,
                removedCount,
                renamedCount,
                skippedFilePaths.Count,
                [.. skippedFilePaths]);
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

    private void IdentifyFiles(Library library, List<IndexedFile> toBeIdentifiedFiles, List<IBaseRequest> backgroundTasks)
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
                        EnrichMusicIdentificationFromTags(file);
                    }
                }

                foreach (var albumGroup in toBeIdentifiedFiles
                    .Where(f => f.Identification is not null)
                    .GroupBy(f => (f.Identification!.AlbumName ?? f.ParentDirectory, f.Identification.ArtistName)))
                {
                    var albumFiles = albumGroup.ToList();
                    backgroundTasks.Add(new CreateBackgroundTaskCommand()
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
                    backgroundTasks.Add(new CreateBackgroundTaskCommand()
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
            var tags = _audioTagReader.ReadTags(file.Path);
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
