namespace K7.Shared.Dtos;

public sealed record ServerMetricsSnapshotDto
{
    public required DateTime Timestamp { get; init; }
    public required double CpuPercent { get; init; }
    public required double MemoryUsedMb { get; init; }
    public required double MemoryTotalMb { get; init; }
    public required double NetworkMbps { get; init; }
    public double GcHeapMb { get; init; }
    public int ThreadCount { get; init; }
    public long UptimeSeconds { get; init; }
    public int OnlineUsersCount { get; init; }
    public int ConnectedDevicesCount { get; init; }
    public IReadOnlyList<ServerDiskVolumeDto> DiskVolumes { get; init; } = [];
}

public sealed record ServerMetricsHistoryDto
{
    public required IReadOnlyList<ServerMetricsSnapshotDto> Snapshots { get; init; }
}
