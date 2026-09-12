namespace K7.Server.Domain.Events;

public class MediaHiddenChangedEvent(
    Guid userId,
    string? userName,
    Guid mediaId,
    bool isHidden,
    bool isSelfExcluded,
    bool isAdminExcluded) : BaseEvent
{
    public Guid UserId { get; } = userId;
    public string? UserName { get; } = userName;
    public Guid MediaId { get; } = mediaId;
    public bool IsHidden { get; } = isHidden;
    public bool IsSelfExcluded { get; } = isSelfExcluded;
    public bool IsAdminExcluded { get; } = isAdminExcluded;
}
