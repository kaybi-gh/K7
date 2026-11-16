namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Notifies connected clients that the home layout of a specific user changed, so every open
/// device loads its home again.
/// </summary>
public interface IUserHomeLayoutNotifier
{
    Task NotifyHomeLayoutUpdatedAsync(string identityUserId, CancellationToken cancellationToken = default);
}
