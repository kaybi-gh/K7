namespace K7.Server.Domain.Events;

/// <summary>
/// Raised outside EF SaveChanges (e.g. background jobs). Published via <c>IDomainEventPublisher</c>.
/// </summary>
public class TranscodeFailedEvent : BaseEvent
{
    public TranscodeFailedEvent(Guid? indexedFileId, string? mediaTitle, string errorMessage)
    {
        IndexedFileId = indexedFileId;
        MediaTitle = mediaTitle;
        ErrorMessage = errorMessage;
    }

    public Guid? IndexedFileId { get; }
    public string? MediaTitle { get; }
    public string ErrorMessage { get; }
}
