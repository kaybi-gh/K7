namespace K7.Shared.Dtos.Notifications;

public sealed record MediaBatchItem
{
    public required Guid MediaId { get; init; }
    public string? Title { get; init; }
    public required string MediaType { get; init; }
    public Guid? LibraryId { get; init; }
}
