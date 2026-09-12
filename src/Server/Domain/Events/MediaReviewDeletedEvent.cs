namespace K7.Server.Domain.Events;

public class MediaReviewDeletedEvent(
    Guid userId,
    string? userName,
    Guid mediaId) : BaseEvent
{
    public Guid UserId { get; } = userId;
    public string? UserName { get; } = userName;
    public Guid MediaId { get; } = mediaId;
}
