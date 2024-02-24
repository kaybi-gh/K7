using MediaClient.Shared.Models;
using MediaClient.Shared.Services;
using Microsoft.AspNetCore.Components;

namespace MediaClient.Shared.Pages;

public partial class Movie
{
    [Parameter]
    public required string Id { get; set; }

    private static MediaItem? _movie;

    protected override void OnInitialized()
    {
        base.OnInitialized();
        _movie = MediaItemServiceMock.All.Where(m => m.Id == Id).First();
    }
}