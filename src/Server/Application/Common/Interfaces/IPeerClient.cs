namespace K7.Server.Application.Common.Interfaces;

public interface IPeerClient
{
    Task SendPeerRequestAsync(string remoteUrl, string localServerName, string localServerUrl, string token, CancellationToken cancellationToken = default);
    Task SendPeerConfirmAsync(string remoteUrl, string token, string clientId, string clientSecret, CancellationToken cancellationToken = default);
    Task<string?> GetAccessTokenAsync(string baseUrl, string clientId, string clientSecret, CancellationToken cancellationToken = default);
}
