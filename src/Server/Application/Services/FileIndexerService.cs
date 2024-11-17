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
public class FileIndexerService : IFileIndexerService
{
    private readonly ILogger<FileIndexerService> _logger;
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public FileIndexerService(ILogger<FileIndexerService> logger, IApplicationDbContext context, ISender sender)
    {
        _logger = logger;
        _context = context;
        _sender = sender;
    }

    public async Task IndexAsync(Library library, CancellationToken cancellationToken)
    {
        List<IndexedFile> indexedFiles = [];
        List<IBaseRequest> backgroundTasks = [];

        try
        {
            _logger.LogInformation($"Starting indexing files of library with library id {library.Id}.");
            var fileInfos = FileInfoHelper.GetAllFileInfosRecursively(library.RootPath);

            foreach (var fileInfo in fileInfos)
            {
                _logger.LogDebug(fileInfo.FullName);
                var indexedFile = fileInfo.ToIndexedFile(library.Id);
                if (indexedFile != null)
                {
                    indexedFiles.Add(indexedFile);
                }
            }

            var (unchangedFiles, addedFiles, removedFiles, renamedFiles) = library.IndexedFiles.CompareTo(indexedFiles);
            var toBeIdentifiedFiles = addedFiles.Concat(unchangedFiles.Where(x => x.Identification == null || !x.MediaId.HasValue));

            _logger.LogInformation($"Found {unchangedFiles.Count()} unchanged files," +
                $"{addedFiles.Count()} added files," +
                $"{removedFiles.Count()} removed files," +
                $"{renamedFiles.Count()} renamed files.\n" +
                $"This makes {toBeIdentifiedFiles.Count()} files to be identified.");

            if (toBeIdentifiedFiles.Any())
            {
                if (library.MediaType == LibraryMediaType.Movie)
                {
                    foreach (var toBeIdentifiedFile in toBeIdentifiedFiles)
                    {
                        if (toBeIdentifiedFile.TryIdentifyMovie(out MediaIdentification? movieIdentification))
                        {
                            toBeIdentifiedFile.Identification = movieIdentification;
                            backgroundTasks.Add(new CreateBackgroundTaskCommand()
                            {
                                Request = new CreateMediaCommand() // TODO - Move to IndexedFileCreatedEventHandler
                                {
                                    IndexedFileId = toBeIdentifiedFile.Id,
                                    MediaType = MediaType.Movie
                                },
                                Priority = BackgroundTaskPriority.Normal,
                                TargetEntityTypeName = nameof(BaseMedia),
                                MaxRetryCount = 5
                            });
                        }
                    }
                }
                else if (library.MediaType == LibraryMediaType.Music)
                {
                    var filesGroupedByParentDirectory = toBeIdentifiedFiles.GroupBy(f => f.ParentDirectory);

                    foreach (var parentDirectory in filesGroupedByParentDirectory)
                    {
                        var addedFilesInSameDirectory = parentDirectory.ToList();
                        foreach (var addedFile in addedFilesInSameDirectory)
                        {
                            addedFile.TryIdentifyMusicTrack(library, addedFilesInSameDirectory);
                        }
                    }
                }
                else if (library.MediaType == LibraryMediaType.Serie)
                {
                    var filesGroupedByParentDirectory = toBeIdentifiedFiles.GroupBy(f => f.ParentDirectory);

                    foreach (var parentDirectory in filesGroupedByParentDirectory)
                    {
                        //addedFile.TryIdentifySerieEpisode(library);
                    }
                }

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
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (removedFiles.Any())
            {
                backgroundTasks.AddRange(removedFiles.Select(x => new CreateBackgroundTaskCommand()
                {
                    Request = new DeleteIndexedFileCommand(x.Id),
                    Priority = BackgroundTaskPriority.Normal,
                    TargetEntityTypeName = nameof(BaseMedia),
                    MaxRetryCount = 5
                }));
            }

            if (renamedFiles.Any())
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

                    if (newFile.TryIdentifyMovie(out MediaIdentification? movieIdentification))
                    {
                        if (movieIdentification != oldFile.Identification)
                        {
                            oldFile.Identification = movieIdentification;
                            backgroundTasks.Add(new CreateMediaCommand()
                            {
                                IndexedFileId = oldFile.Id,
                                MediaType = MediaType.Movie
                            });
                        }
                    }

                    _context.IndexedFiles.Update(oldFile);
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (var request in backgroundTasks)
            {
                await _sender.Send(request, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error indexing files in directory {library.RootPath}: {ex.Message}");
        }
    }
}
