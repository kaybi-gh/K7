using System.Security.Cryptography;
using K7.Server.Application.Models;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities;

namespace K7.Server.Application.Extensions;

public static class FileInfoExtensions
{
    public static bool IsSupportedFile(this FileInfo fileInfo)
    {
        return Constants.MediaFiles.Contains(fileInfo.Extension, StringComparer.OrdinalIgnoreCase);
    }

    public static ScannedFileEntry ToScannedFileEntry(this FileInfo fileInfo)
    {
        return new ScannedFileEntry
        {
            Path = fileInfo.FullName,
            Name = Path.GetFileNameWithoutExtension(fileInfo.Name),
            Extension = fileInfo.Extension,
            ParentDirectory = fileInfo.Directory?.Name,
            Size = fileInfo.Length,
            LastWriteTimeUtc = fileInfo.LastWriteTimeUtc
        };
    }

    public static uint ComputeFileHash(this FileInfo fileInfo, CancellationToken cancellationToken = default)
    {
        // Changing count will invalidate every IndexedFile seed
        const int kiloBytesCount = 10;
        const int bufferSize = kiloBytesCount * 1024;

        cancellationToken.ThrowIfCancellationRequested();

        var buffer = new byte[bufferSize];

        using var fileStream = fileInfo.OpenRead();
        var bytesRead = fileStream.ReadAsync(buffer.AsMemory(0, bufferSize), cancellationToken)
            .AsTask()
            .GetAwaiter()
            .GetResult();

        if (bytesRead == 0)
            return 0;

        var hashBytes = SHA256.HashData(buffer.AsSpan(0, bytesRead));

        // Combine content hash with file size for a stronger seed
        return BitConverter.ToUInt32(hashBytes) ^ (uint)fileInfo.Length;
    }

    public static IndexedFile ToIndexedFile(this ScannedFileEntry entry, Guid libraryId, uint hash)
    {
        return new IndexedFile
        {
            Id = Guid.NewGuid(),
            LibraryId = libraryId,
            Name = entry.Name,
            Extension = entry.Extension,
            Path = entry.Path,
            ParentDirectory = entry.ParentDirectory,
            Hash = hash,
            Size = entry.Size,
            LastWriteTimeUtc = entry.LastWriteTimeUtc
        };
    }

    public static IndexedFile? ToIndexedFile(this FileInfo fileInfo, Guid libraryId, CancellationToken cancellationToken = default)
    {
        if (!fileInfo.IsSupportedFile())
        {
            return null;
        }

        return fileInfo.ToScannedFileEntry().ToIndexedFile(libraryId, fileInfo.ComputeFileHash(cancellationToken));
    }
}
