using MediaClient.Shared.Domain.Models;
using MediaClient.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace MediaClient.Shared.Pages;

public partial class Library
{
    [Parameter]
    public required string Id { get; set; }

    private List<MediaItem> MediaItems { get; set; } = [];

    protected override void OnInitialized()
    {
        MediaItems = MediaItemServiceMock.All;
        base.OnInitialized();
    }
}