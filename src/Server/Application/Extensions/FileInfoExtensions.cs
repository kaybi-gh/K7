using System.Security.Cryptography;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Extensions;
public static class FileInfoExtensions
{
    public static bool IsSupportedFile(this FileInfo fileInfo)
    {
        return Constants.MediaFiles.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase);
    }

    public static uint ComputeFileHash(this FileInfo fileInfo)
    {
        // Changing count will invalidate every IndexedFile seed
        const int kiloBytesCount = 10;
        const int bufferSize = kiloBytesCount * 1024;

        byte[] buffer = new byte[bufferSize];
        int bytesRead;

        using var fileStream = fileInfo.OpenRead();
        using var bufferedStream = new BufferedStream(fileStream, bufferSize);

        bytesRead = bufferedStream.Read(buffer, 0, bufferSize);
        if (bytesRead == 0) return 0;
        byte[] hashBytes = SHA256.HashData(buffer.AsSpan(0, bytesRead));

        // Combine content hash with file size for a stronger seed
        return BitConverter.ToUInt32(hashBytes) ^ (uint)fileInfo.Length;
    }

    public static IndexedFile? ToIndexedFile(this FileInfo fileInfo, Guid libraryId)
    {
        if (!fileInfo.IsSupportedFile())
        {
            return null;
        }

        return new IndexedFile()
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            Name = Path.GetFileNameWithoutExtension(fileInfo.Name),
            Extension = fileInfo.Extension,
            Path = fileInfo.FullName,
            ParentDirectory = fileInfo.Directory?.Name,
            Hash = fileInfo.ComputeFileHash(),
            Size = fileInfo.Length
        };
    }
}
