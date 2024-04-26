using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Extensions;
using MediaServer.Application.Features.Medias.Commands.CreateMedia;
using MediaServer.Application.Helpers;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;
using MediaServer.Domain.Interfaces;
using MediaServer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MediaServer.Application.Services;
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
        List<CreateMediaCommand> createMediaCommands = [];

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
                            createMediaCommands.Add(new CreateMediaCommand()
                            {
                                IndexedFile = toBeIdentifiedFile,
                                MediaType = MediaType.Movie
                            });
                        }
                    }
                }
                else if (library.MediaType == LibraryMediaType.Music)
                {
                    var filesGroupedByParentDirectory = addedFiles.GroupBy(f => f.ParentDirectory);

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
                    var filesGroupedByParentDirectory = addedFiles.GroupBy(f => f.ParentDirectory);

                    foreach (var parentDirectory in filesGroupedByParentDirectory)
                    {
                        //addedFile.TryIdentifySerieEpisode(library);
                    }
                }

                _context.IndexedFiles.AddRange(addedFiles);
                foreach (var file in addedFiles)
                {
                    file.AddDomainEvent(new IndexedFileCreatedEvent(file));
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            if (removedFiles.Any())
            {
                _context.IndexedFiles.RemoveRange(removedFiles);
                foreach (var file in removedFiles)
                {
                    file.AddDomainEvent(new IndexedFileDeletedEvent(file));
                }
                // TODO - Clean metadas, pictures, stats if asked?
                await _context.SaveChangesAsync(cancellationToken);
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
                            createMediaCommands.Add(new CreateMediaCommand()
                            {
                                IndexedFile = oldFile,
                                MediaType = MediaType.Movie
                            });
                        }
                    }

                    _context.IndexedFiles.Update(oldFile);
                }
                await _context.SaveChangesAsync(cancellationToken);
            }

            foreach (var command in createMediaCommands)
            {
                await _sender.Send(command, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error indexing files in directory {library.RootPath}: {ex.Message}");
        }
    }
}
