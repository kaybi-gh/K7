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

    public static string GenerateSeedFromFirstNBytes(this FileInfo fileInfo, int nKiloBytes)
    {
        const int bufferSize = 1024; // Buffer size in bytes
        byte[] buffer = new byte[bufferSize];
        int bytesRead;

        using (var fileStream = fileInfo.OpenRead())
        using (var bufferedStream = new BufferedStream(fileStream, bufferSize))
        {
            using (var sha256 = SHA256.Create())
            {
                bytesRead = bufferedStream.Read(buffer, 0, Math.Min(bufferSize, nKiloBytes * 1024));
                if (bytesRead == 0) return ""; // File is empty, return seed as 0

                // Calculate hash for the first n kilobytes
                byte[] hashBytes = sha256.ComputeHash(buffer, 0, bytesRead);

                // Prefix the hash with the file size
                long fileSize = fileInfo.Length;
                byte[] fileSizeBytes = BitConverter.GetBytes(fileSize);
                byte[] combinedBytes = new byte[hashBytes.Length + fileSizeBytes.Length];
                Array.Copy(fileSizeBytes, 0, combinedBytes, 0, fileSizeBytes.Length);
                Array.Copy(hashBytes, 0, combinedBytes, fileSizeBytes.Length, hashBytes.Length);

                // Convert the first 4 bytes of the combined hash to uint to use as seed
                return BitConverter.ToString(combinedBytes, 0);
            }
        }
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
                    Hash = fileInfo.GenerateSeedFromFirstNBytes(10),
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
