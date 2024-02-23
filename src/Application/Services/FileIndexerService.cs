using MediaServer.Application.Common.Interfaces;
using MediaServer.Application.Extensions;
using MediaServer.Application.Helpers;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Medias;
using MediaServer.Domain.Enums;
using MediaServer.Domain.Events;
using MediaServer.Domain.Interfaces;

namespace MediaServer.Application.Services;
public class FileIndexerService : IFileIndexerService
{
    private readonly IApplicationDbContext _context;

    public FileIndexerService(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task IndexAsync(Library library, CancellationToken cancellationToken)
    {
        List<IndexedFile> indexedFiles = [];
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

            var (unchangedFiles, addedFiles, removedFiles) = library.IndexedFiles.CompareTo(indexedFiles);

            if (addedFiles.Any())
            {
                if (library.MediaType == LibraryMediaType.Movie)
                {
                    List<Movie> addedMovies = [];
                    foreach (var addedFile in addedFiles)
                    {
                        if (addedFile.TryIdentifyMovie(library, out Movie? movie))
                        {
                            addedMovies.Add(movie);
                        }
                    }
                    _context.Medias.AddRange(addedMovies);
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

            library.IndexedFiles = indexedFiles;
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error indexing files in directory {library.RootPath}: {ex.Message}");
        }
    }
}
