namespace K7.Shared.Dtos.Requests;

public sealed record ReassignPlaybackHistoryRequest
{
    public Guid? SharedProfileId { get; init; }
}
