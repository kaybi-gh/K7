using K7.Shared.Dtos.Entities;

namespace K7.Server.Application.Common.Interfaces;

public interface IPeerClient
{
    Task SendPeerRequestAsync(string remoteUrl, string localServerName, string localServerUrl, string token, CancellationToken cancellationToken = default);
    Task SendPeerConfirmAsync(string remoteUrl, string token, string clientId, string clientSecret, CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(string baseUrl, string clientId, string clientSecret, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeerLibraryDto>> GetRemoteLibrariesAsync(string baseUrl, string accessToken, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PeerMediaDto>> GetRemoteMediaAsync(string baseUrl, string accessToken, Guid libraryId, CancellationToken cancellationToken = default);
    Task<Stream?> GetRemoteStreamAsync(string baseUrl, string accessToken, Guid remoteFileId, CancellationToken cancellationToken = default);
}
