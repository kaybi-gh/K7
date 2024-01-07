using System.Security.Cryptography;
using MediaServer.Domain.Constants;
using MediaServer.Domain.Entities;
using MediaServer.Domain.Entities.Files;

namespace MediaServer.Application.Extensions;
public static class FileInfoExtensions
{
    public static MediaFile? ToMediaFile(this FileInfo fileInfo, int libraryId)
    {
        try
        {
            if (fileInfo.IsMediaFile())
            {
                MediaFile mediaFile = new()
                {
                    LibraryId = libraryId,
                    Name = fileInfo.Name,
                    Extension = fileInfo.Extension,
                    Path = fileInfo.FullName,
                    ParentDirectory = fileInfo.Directory?.Name,
                    Hash = fileInfo.ComputeFileHash(),
                    Size = fileInfo.Length,
                    IsIdentified = false
                };

                return mediaFile;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error creating MediaFile for {fileInfo.FullName}: {ex.Message}");
        }

        return null;
    }

    public static bool IsMediaFile(this FileInfo fileInfo)
    {
        return FileExtensions.GetAll().Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase);
    }

    public static string ComputeFileHash(this FileInfo fileInfo)
    {
        using var stream = new BufferedStream(fileInfo.OpenRead(), 1200000);
        byte[] hashBytes = SHA256.HashData(stream);
        return BitConverter.ToString(hashBytes).Replace("-", "").ToLower();
    }
}
