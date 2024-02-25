using MediaClient.Shared.Models;
using MediaClient.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace MediaClient.Shared.Pages;

public partial class Movie
{
    [Parameter]
    public required string Id { get; set; }

    private static MediaItem? _movie;
    private bool _isSmallDevice;
    private bool _overviewExpanded = false;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _movie = MediaItemServiceMock.All.Where(m => m.Id == Id).First();
    }

    private void ToggleDeviceBreakpoint(bool isBreakpointXs)
    {
        _isSmallDevice = isBreakpointXs;
        StateHasChanged();
    }
    private void ToggleOverview()
    {
        _overviewExpanded = !_overviewExpanded;
        StateHasChanged();
    }
}