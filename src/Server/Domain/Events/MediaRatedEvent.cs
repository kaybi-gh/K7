namespace K7.Server.Domain.Events;

public class MediaRatedEvent(
    Guid userId,
    string? userName,
    Guid mediaId,
    int ratingValue,
    bool isNew) : BaseEvent
{
    public Guid UserId { get; } = userId;
    public string? UserName { get; } = userName;
    public Guid MediaId { get; } = mediaId;
    public int RatingValue { get; } = ratingValue;
    public bool IsNew { get; } = isNew;
}
