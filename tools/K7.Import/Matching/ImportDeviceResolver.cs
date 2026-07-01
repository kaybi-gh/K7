using K7.Import.Clients;
using K7.Import.Models;
using K7.Shared.Dtos.Requests;

namespace K7.Import.Matching;

public sealed class ImportDeviceResolver
{
    private readonly K7ApiClient _k7Client;

    public ImportDeviceResolver(K7ApiClient k7Client)
    {
        _k7Client = k7Client;
    }

    public static string BuildDeviceKey(SourcePlayEntry entry)
    {
        return string.Join('|',
            entry.DeviceName ?? string.Empty,
            entry.Platform ?? string.Empty,
            entry.Player ?? string.Empty);
    }

    public async Task<Dictionary<string, Guid>> ResolveDevicesAsync(
        IEnumerable<SourcePlayEntry> playEntries,
        CancellationToken cancellationToken = default)
    {
        var descriptors = playEntries
            .Select(entry => new
            {
                Key = BuildDeviceKey(entry),
                entry.DeviceName,
                entry.Platform,
                entry.Player
            })
            .Where(d => !string.IsNullOrEmpty(d.DeviceName)
                || !string.IsNullOrEmpty(d.Platform)
                || !string.IsNullOrEmpty(d.Player))
            .DistinctBy(d => d.Key)
            .Select(d => new BulkResolveImportDevicesRequest.ImportDeviceDescriptor
            {
                Key = d.Key,
                DeviceName = d.DeviceName,
                Platform = d.Platform,
                Player = d.Player
            })
            .ToList();

        if (descriptors.Count == 0)
            return new Dictionary<string, Guid>();

        var results = await _k7Client.BulkResolveImportDevicesAsync(descriptors, cancellationToken);

        return results
            .Where(r => r.DeviceId.HasValue)
            .ToDictionary(r => r.Key, r => r.DeviceId!.Value);
    }
}
