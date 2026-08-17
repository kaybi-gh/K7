namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Notifies connected clients about user rating changes for a specific user.
/// </summary>
public interface IUserRatingNotifier
{
    Task NotifyUserRatingUpdatedAsync(
        string identityUserId,
        Guid mediaId,
        int value,
        CancellationToken cancellationToken = default);
}
