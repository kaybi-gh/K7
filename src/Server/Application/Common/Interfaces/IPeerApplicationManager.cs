namespace K7.Server.Application.Common.Interfaces;

public interface IPeerApplicationManager
{
    Task<string> CreatePeerApplicationAsync(string clientId, string clientSecret, string displayName, CancellationToken cancellationToken = default);
    Task DeletePeerApplicationAsync(string clientId, CancellationToken cancellationToken = default);
}
