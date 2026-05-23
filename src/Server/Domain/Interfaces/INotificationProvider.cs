namespace K7.Server.Domain.Interfaces;

public interface INotificationProvider
{
    Task<bool> SendAsync(string configJson, string payload, CancellationToken cancellationToken = default);
}
