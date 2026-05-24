using K7.Server.Domain.Enums;

namespace K7.Server.Domain.Events;

public class PlaybackStateChangedEvent(
    PlaybackState state,
    PlaybackState previousState,
    Guid userId,
    string? userName,
    Guid mediaId,
    string mediaTitle,
    string mediaType,
    Guid sessionId,
    double position,
    double duration,
    string? libraryTitle,
    string? deviceName,
    string? deviceType) : BaseEvent
{
    public PlaybackState State { get; } = state;
    public PlaybackState PreviousState { get; } = previousState;
    public Guid UserId { get; } = userId;
    public string? UserName { get; } = userName;
    public Guid MediaId { get; } = mediaId;
    public string MediaTitle { get; } = mediaTitle;
    public string MediaType { get; } = mediaType;
    public Guid SessionId { get; } = sessionId;
    public double Position { get; } = position;
    public double Duration { get; } = duration;
    public string? LibraryTitle { get; } = libraryTitle;
    public string? DeviceName { get; } = deviceName;
    public string? DeviceType { get; } = deviceType;
}
