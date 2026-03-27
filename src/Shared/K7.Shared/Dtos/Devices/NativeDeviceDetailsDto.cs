namespace K7.Shared.Dtos.Devices;

public sealed record NativeDeviceDetailsDto
{
    public string? RawModel { get; init; }
    public string? RawManufacturer { get; init; }
    public string? RawName { get; init; }
    public string? RawVersion { get; init; }
    public string? RawPlatform { get; init; }
    public string? RawIdiom { get; init; }
    public string? RawDeviceType { get; init; }

}
