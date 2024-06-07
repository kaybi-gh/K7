using MediaClient.Shared.Domain.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace MediaClient.Shared.Pages;

public partial class Movie
{
    [Parameter]
    public required string Id { get; set; }

    private static Domain.Models.Movie? _movie;
    private static MediaPosterViewModel? _mediaPoster;
    private bool _isSmallDevice;
    private bool _overviewExpanded;

    protected override async Task OnInitializedAsync()
    {
        _movie = await MediaServerService.GetMovieAsync(Guid.Parse(Id));
        if (_movie != null)
        {
            _mediaPoster = new MediaPosterViewModel()
            {
                Id = _movie.Id,
                Title = _movie.Title,
                PosterPictureHref = _movie.PosterPictureHref
            };
        }
        base.OnInitialized();
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