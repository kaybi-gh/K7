namespace K7.Shared.Interfaces;

/// <summary>
/// Client-side interface for user rating SignalR updates.
/// </summary>
public interface IUserRatingClient
{
    /// <summary>
    /// Receives a user rating change. Value is 0-10; 0 means the rating was cleared.
    /// </summary>
    Task ReceiveUserRatingUpdated(Guid mediaId, int value);
}
