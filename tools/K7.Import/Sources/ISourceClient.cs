using K7.Import.Models;

namespace K7.Import.Sources;

public interface ISourceClient
{
    Task<SourceServerInfo> ValidateConnectionAsync(CancellationToken cancellationToken = default);
    Task<List<SourceUser>> GetUsersAsync(CancellationToken cancellationToken = default);
    Task<List<SourceLibrary>> GetLibrariesAsync(CancellationToken cancellationToken = default);
    Task<List<SourceMediaItem>> GetLibraryItemsAsync(string libraryId, string userId, CancellationToken cancellationToken = default);
    Task<List<SourcePlaylist>> GetPlaylistsAsync(string userId, CancellationToken cancellationToken = default);
}
