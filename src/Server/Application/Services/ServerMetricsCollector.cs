using System.Collections.Concurrent;
using System.Diagnostics;
using K7.Shared.Dtos;

namespace K7.Server.Application.Services;

public interface IServerMetricsCollector
{
    void RecordSample(double networkMbps);
    ServerMetricsHistoryDto GetHistory();
    ServerMetricsSnapshotDto GetCurrentSnapshot(double networkMbps);
    void PrimeCpuBaseline();
}

public sealed class ServerMetricsCollector(
    IActiveStreamTracker activeStreamTracker,
    IHubPresenceTracker hubPresenceTracker,
    IServerDiskMetricsProvider diskMetricsProvider) : IServerMetricsCollector
{
    private const int MaxSnapshots = 72;
    private static readonly TimeSpan SampleInterval = TimeSpan.FromSeconds(5);
    private readonly ConcurrentQueue<ServerMetricsSnapshotDto> _history = new();
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly DateTime _processStartUtc = DateTime.UtcNow;
    private DateTime _lastSampleAt = DateTime.MinValue;
    private TimeSpan _lastCpuTime = TimeSpan.Zero;
    private DateTime _lastCpuSampleAt = DateTime.UtcNow;

    public void PrimeCpuBaseline()
    {
        _process.Refresh();
        _lastCpuTime = _process.TotalProcessorTime;
        _lastCpuSampleAt = DateTime.UtcNow;
    }

    public void RecordSample(double networkMbps)
    {
        var now = DateTime.UtcNow;
        if (now - _lastSampleAt < SampleInterval)
            return;

        _lastSampleAt = now;
        var snapshot = GetCurrentSnapshot(networkMbps);
        _history.Enqueue(snapshot);

        while (_history.Count > MaxSnapshots && _history.TryDequeue(out _))
        {
        }
    }

    public ServerMetricsHistoryDto GetHistory() =>
        new() { Snapshots = _history.ToArray() };

    public ServerMetricsSnapshotDto GetCurrentSnapshot(double networkMbps)
    {
        _process.Refresh();

        var gcInfo = GC.GetGCMemoryInfo();
        var usedMemoryMb = _process.WorkingSet64 / (1024d * 1024d);
        var totalMemoryMb = gcInfo.TotalAvailableMemoryBytes / (1024d * 1024d);
        if (totalMemoryMb <= 0)
            totalMemoryMb = Math.Max(usedMemoryMb, 1);

        var gcHeapMb = GC.GetTotalMemory(false) / (1024d * 1024d);
        var cpuPercent = GetCpuPercent();

        if (networkMbps <= 0)
            networkMbps = GetActiveStreamNetworkMbps();

        var diskVolumes = diskMetricsProvider.GetVolumes();

        return new ServerMetricsSnapshotDto
        {
            Timestamp = DateTime.UtcNow,
            CpuPercent = cpuPercent,
            MemoryUsedMb = Math.Round(usedMemoryMb, 1),
            MemoryTotalMb = Math.Round(totalMemoryMb, 1),
            NetworkMbps = Math.Round(networkMbps, 2),
            GcHeapMb = Math.Round(gcHeapMb, 1),
            ThreadCount = _process.Threads.Count,
            UptimeSeconds = (long)(DateTime.UtcNow - _processStartUtc).TotalSeconds,
            OnlineUsersCount = hubPresenceTracker.GetOnlineUserCount(),
            ConnectedDevicesCount = hubPresenceTracker.GetConnectedDeviceCount(),
            DiskVolumes = diskVolumes
        };
    }

    private double GetActiveStreamNetworkMbps()
    {
        var totalKbps = activeStreamTracker.GetActiveStreams()
            .Select(s => s.StreamDecision?.Bitrate ?? 0)
            .Sum();

        return totalKbps / 1000d;
    }

    private double GetCpuPercent()
    {
        try
        {
            var now = DateTime.UtcNow;
            var currentCpuTime = _process.TotalProcessorTime;
            var elapsedMs = (now - _lastCpuSampleAt).TotalMilliseconds;

            if (elapsedMs <= 0 || _lastCpuTime == TimeSpan.Zero)
            {
                _lastCpuTime = currentCpuTime;
                _lastCpuSampleAt = now;
                return 0;
            }

            var cpuUsedMs = (currentCpuTime - _lastCpuTime).TotalMilliseconds;
            _lastCpuTime = currentCpuTime;
            _lastCpuSampleAt = now;

            return Math.Round(Math.Clamp(cpuUsedMs / (elapsedMs * Environment.ProcessorCount) * 100d, 0, 100), 1);
        }
        catch
        {
            return 0;
        }
    }
}
