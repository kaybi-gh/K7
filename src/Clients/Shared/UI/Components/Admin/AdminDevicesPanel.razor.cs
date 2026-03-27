using K7.Shared.Dtos.Devices;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Admin;

public partial class AdminDevicesPanel
{
    [Inject] private IDeviceApiService K7ServerService { get; set; } = default!;

    private bool _isLoading = true;
    private K7.Shared.Dtos.PaginatedListDto<DeviceDto>? _devices;

    protected override async Task OnInitializedAsync()
    {
        _isLoading = true;
        try
        {
            _devices = await K7ServerService.GetDevicesAsync();
        }
        catch { }
        finally
        {
            _isLoading = false;
        }
    }
}
