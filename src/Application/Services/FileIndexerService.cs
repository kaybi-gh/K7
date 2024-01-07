using MediaServer.Application.Extensions;
using MediaServer.Application.Helper;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Files;
using MediaServer.Domain.Interfaces;

namespace MediaServer.Application.Services;
public class FileIndexerService : IFileIndexerService
{
    public FileIndexerService()
    {

    }

    public void IndexMediaFiles(Library library)
    {
        List<MediaFile> mediaFiles = [];
        try
        {
            foreach (var fileInfo in FileInfoHelper.GetAllFileInfosRecursively(library.RootPath))
            {
                var mediaFile = fileInfo.ToMediaFile(library.Id);
                if (mediaFile != null)
                {
                    mediaFiles.Add(mediaFile);
                }
            }

            var (addedFiles, removedFiles, unchangedFiles) = library.Files.CompareTo(mediaFiles);

            if (addedFiles.Any())
            {

            }

            if (removedFiles.Any())
            {

            }

            library.Files = mediaFiles;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error indexing files in directory {library.RootPath}: {ex.Message}");
        }
    }
}
