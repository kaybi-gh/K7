namespace K7.Shared.Dtos;

public sealed record ServerDiskVolumeDto
{
    public required string Label { get; init; }
    public required double UsedGb { get; init; }
    public required double TotalGb { get; init; }
    public required double FreePercent { get; init; }
}
