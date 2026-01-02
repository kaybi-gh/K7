namespace K7.Shared.Dtos.Requests;

public sealed record GetIndexedFileStreamsUriQuery
{
    public required Guid Id { get; set; }
    public string? DeviceId { get; set; }
}
