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

    public static uint ComputeFileHash(this FileInfo fileInfo)
    {
        // Changing count will invalidate every IndexedFile seed
        const int kiloBytesCount = 10;
        const int bufferSize = 1024;
        
        byte[] buffer = new byte[bufferSize];
        int bytesRead;

        using var fileStream = fileInfo.OpenRead();
        using var bufferedStream = new BufferedStream(fileStream, bufferSize);

        bytesRead = bufferedStream.Read(buffer, 0, Math.Min(bufferSize, kiloBytesCount * 1024));
        if (bytesRead == 0) return 0;
        byte[] hashBytes = SHA256.HashData(buffer.AsSpan(0, bytesRead));

        // Prefix the hash with the file size
        long fileSize = fileInfo.Length;
        byte[] fileSizeBytes = BitConverter.GetBytes(fileSize);
        byte[] combinedBytes = new byte[hashBytes.Length + fileSizeBytes.Length];
        Array.Copy(fileSizeBytes, 0, combinedBytes, 0, fileSizeBytes.Length);
        Array.Copy(hashBytes, 0, combinedBytes, fileSizeBytes.Length, hashBytes.Length);

        // Convert the first 4 bytes of the combined hash to uint to use as seed
        return BitConverter.ToUInt32(combinedBytes, 0);
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
