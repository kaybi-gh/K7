using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Layout;

public partial class ExploreLayout
{
    [Inject] private IDeviceService DeviceService { get; set; } = default!;

    private bool _isTv;

    protected override async Task OnInitializedAsync()
    {
        _isTv = await DeviceService.GetDeviceTypeAsync() == DeviceType.TV;
    }
}
