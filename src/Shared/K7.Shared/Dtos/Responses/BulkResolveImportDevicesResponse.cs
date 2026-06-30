namespace K7.Shared.Dtos.Responses;

public sealed record BulkResolveImportDevicesResponse
{
    public required IReadOnlyList<DeviceMatchResult> Results { get; init; }

    public sealed record DeviceMatchResult
    {
        public required string Key { get; init; }
        public Guid? DeviceId { get; init; }
        public bool WasCreated { get; init; }
    }
}
