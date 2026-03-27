using K7.Shared.Dtos.Devices;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin;

public partial class Devices
{
    [Inject] private IDeviceApiService K7ServerService { get; set; } = default!;

    private bool isLoading = true;
    private K7.Shared.Dtos.PaginatedListDto<DeviceDto>? devices;

    protected override async Task OnInitializedAsync()
    {
        try
        {
            devices = await K7ServerService.GetDevicesAsync();
        }
        catch { }
        finally
        {
            isLoading = false;
        }
    }
}
