using System.Security.Cryptography;
using MediaServer.Domain.Entities;

namespace MediaServer.Tests.Helpers.Samples;

public static class IndexedFilesSamples
{
    public static readonly List<IndexedFile> MusicFiles = [
        new IndexedFile()
        {
            Id = Guid.NewGuid(),
            LibraryId = Guid.NewGuid(),
            Extension = ".mp3",
            Hash =  BitConverter.ToUInt32(SHA256.HashData(Guid.NewGuid().ToByteArray()), 0)!,
            Name = "name1",
            Path = "/path1/name1.mp3",
            Size = 32
        }
    ];
}
