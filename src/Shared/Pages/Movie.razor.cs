using MediaClient.Shared.Domain.Models;
using MediaClient.Shared.Services;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace MediaClient.Shared.Pages;

public partial class Movie
{
    [Parameter]
    public required string Id { get; set; }

    private static MediaItem? _movie;
    private bool _isSmallDevice;
    private bool _overviewExpanded;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _movie = MediaItemServiceMock.All.Where(m => m.Id == Id).First();
    }

    private void ScreenResized(Breakpoint breakpoint)
    {
        _isSmallDevice = breakpoint == Breakpoint.Xs;
    }

    private void ToggleOverview()
    {
        _overviewExpanded = !_overviewExpanded;
    }
}