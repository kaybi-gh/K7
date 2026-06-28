namespace K7.Shared.Dtos.Devices;

public sealed record DeviceCodecSummaryDto
{
    public required string[] Containers { get; init; }
    public required string[] AudioCodecs { get; init; }
    public required string[] VideoCodecs { get; init; }
}
