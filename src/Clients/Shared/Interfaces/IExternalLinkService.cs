namespace K7.Clients.Shared.Interfaces;

public interface IExternalLinkService
{
    Task<bool> OpenAsync(string url, CancellationToken cancellationToken = default);
}
