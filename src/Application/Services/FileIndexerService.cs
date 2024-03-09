using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Extensions;
using MediaServer.Application.Features.Medias.Commands.CreateMedia;
using MediaServer.Application.Helpers;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;
using MediaServer.Domain.Interfaces;
using MediaServer.Domain.ValueObjects;

namespace MediaServer.Application.Services;
public class FileIndexerService : IFileIndexerService
{
    private readonly IApplicationDbContext _context;
    private readonly ISender _sender;

    public FileIndexerService(IApplicationDbContext context, ISender sender)
    {
        _context = context;
        _sender = sender;
    }

    public async Task IndexAsync(Library library, CancellationToken cancellationToken)
    {
        List<IndexedFile> indexedFiles = [];
        List<Task> mediaCreationTasks = [];
        try
        {
            var fileInfos = FileInfoHelper.GetAllFileInfosRecursively(library.RootPath);

            foreach (var fileInfo in fileInfos)
            {
                var indexedFile = fileInfo.ToIndexedFile(library.Id);
                if (indexedFile != null)
                {
                    indexedFiles.Add(indexedFile);
                }
            }

            var (_, addedFiles, removedFiles, renamedFiles) = library.IndexedFiles.CompareTo(indexedFiles);

            library.IndexedFiles = indexedFiles;
            await _context.SaveChangesAsync(cancellationToken);

            // TODO - Déplacer dans une logique évenementielle
            if (addedFiles.Any())
            {
                if (library.MediaType == LibraryMediaType.Movie)
                {
                    foreach (var addedFile in addedFiles)
                    {
                        if (addedFile.TryIdentifyMovie(out MediaIdentification? movieIdentification))
                        {
                            mediaCreationTasks.Add(_sender.Send(new CreateMediaCommand()
                            {
                                IndexedFileIds = [addedFile.Id],
                                LibraryIds = [library.Id],
                                MediaIdentification = movieIdentification,
                                MediaType = MediaType.Movie
                            }, cancellationToken));
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

                await _context.IndexedFiles.AddRangeAsync(addedFiles, cancellationToken);
                foreach (var file in addedFiles)
                {
                    file.AddDomainEvent(new IndexedFileCreatedEvent(file));
                }
            }

            if (removedFiles.Any())
            {
                _context.IndexedFiles.RemoveRange(removedFiles);
                foreach (var file in removedFiles)
                {
                    file.AddDomainEvent(new IndexedFileDeletedEvent(file));
                }
            }

            await Task.WhenAll(mediaCreationTasks);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error indexing files in directory {library.RootPath}: {ex.Message}");
        }
    }
}
