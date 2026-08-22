using K7.Server.Domain.Entities;
using K7.Server.Domain.Entities.Federation;
using K7.Server.Domain.Enums;
using K7.Server.Infrastructure.Database.Context.Data;

namespace K7.Tests.Helpers.Samples;

public static class RemoteIndexedFilesSamples
{
    public static (Guid LibraryId, Guid PeerServerId) EnsureLibraryAndPeer(ApplicationDbContext context)
    {
        var libraryId = Guid.NewGuid();
        var libraryGroupId = Guid.NewGuid();
        var peerServerId = Guid.NewGuid();

        context.PeerServers.Add(new PeerServer
        {
            Id = peerServerId,
            Name = "Peer",
            BaseUrl = "https://peer.example"
        });
        context.LibraryGroups.Add(new LibraryGroup
        {
            Id = libraryGroupId,
            Title = "TV",
            MediaType = LibraryMediaType.Serie
        });
        context.Libraries.Add(new Library
        {
            Id = libraryId,
            LibraryGroupId = libraryGroupId,
            MediaType = LibraryMediaType.Serie,
            MetadataProviderName = "tmdb",
            MetadataLanguage = "fr",
            MetadataFallbackLanguage = "en",
            Title = "TV"
        });

        return (libraryId, peerServerId);
    }

    public static RemoteIndexedFile Create(Guid mediaId, Guid libraryId, Guid peerServerId) =>
        new()
        {
            PeerServerId = peerServerId,
            RemoteFileId = Guid.NewGuid(),
            Name = "episode.mkv",
            Extension = ".mkv",
            Size = 1024,
            MediaId = mediaId,
            RemoteMediaId = Guid.NewGuid(),
            LibraryId = libraryId,
            RemoteLibraryId = Guid.NewGuid()
        };
}
