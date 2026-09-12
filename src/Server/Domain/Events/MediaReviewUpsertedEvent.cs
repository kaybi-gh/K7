namespace K7.Server.Domain.Events;

public class MediaReviewUpsertedEvent(
    Guid userId,
    string? userName,
    Guid mediaId,
    int ratingValue,
    string text,
    string? emoji,
    bool isNew) : BaseEvent
{
    public Guid UserId { get; } = userId;
    public string? UserName { get; } = userName;
    public Guid MediaId { get; } = mediaId;
    public int RatingValue { get; } = ratingValue;
    public string Text { get; } = text;
    public string? Emoji { get; } = emoji;
    public bool IsNew { get; } = isNew;
}
