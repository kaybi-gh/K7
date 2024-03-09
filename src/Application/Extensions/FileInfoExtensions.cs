using System.Security.Cryptography;
using MediaServer.Domain.Constants;
using MediaServer.Domain.Entities;

namespace MediaServer.Application.Extensions;
public static class FileInfoExtensions
{
    public static bool IsSupportedFile(this FileInfo fileInfo)
    {
        return FileExtensions.MediaFiles.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase);
    }

    public static string ComputeFileHash(this FileInfo fileInfo)
    {
        using var stream = new BufferedStream(fileInfo.OpenRead(), 1200000);
        byte[] hashBytes = SHA256.HashData(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }

    public static IndexedFile? ToIndexedFile(this FileInfo fileInfo, int libraryId)
    {
        try
        {
            if (fileInfo.IsSupportedFile())
            {
                IndexedFile indexedFile = new()
                {
                    LibraryId = libraryId,
                    Name = Path.GetFileNameWithoutExtension(fileInfo.Name),
                    Extension = fileInfo.Extension,
                    Path = fileInfo.FullName,
                    ParentDirectory = fileInfo.Directory?.Name,
                    Hash = fileInfo.ComputeFileHash(),
                    Size = fileInfo.Length
                };

                return indexedFile;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating IndexedFile for {fileInfo.FullName}: {ex.Message}");
        }

        return null;
    }
}
