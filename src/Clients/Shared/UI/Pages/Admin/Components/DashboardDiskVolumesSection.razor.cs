using K7.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Components;

public partial class DashboardDiskVolumesSection
{
    [Parameter] public IReadOnlyList<ServerDiskVolumeDto> Volumes { get; set; } = [];

    private string _volumesFingerprint = string.Empty;

    protected override bool ShouldRender()
    {
        var fingerprint = BuildFingerprint(Volumes);
        if (fingerprint == _volumesFingerprint)
            return false;

        _volumesFingerprint = fingerprint;
        return true;
    }

    private static string BuildFingerprint(IReadOnlyList<ServerDiskVolumeDto> volumes) =>
        string.Join('|', volumes.Select(v =>
            $"{v.Label}:{v.UsedGb:0.#}:{v.TotalGb:0.#}:{v.FreePercent:0.#}"));
}
