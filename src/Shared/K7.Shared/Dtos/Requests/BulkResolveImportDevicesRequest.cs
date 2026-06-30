namespace K7.Shared.Dtos.Requests;

public sealed record BulkResolveImportDevicesRequest
{
    public required IReadOnlyList<ImportDeviceDescriptor> Items { get; init; }

    public sealed record ImportDeviceDescriptor
    {
        public required string Key { get; init; }
        public string? DeviceName { get; init; }
        public string? Platform { get; init; }
        public string? Player { get; init; }
    }
}
