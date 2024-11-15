using MediaClient.Shared.Domain.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace MediaClient.Shared.Pages;

public partial class Person
{
    [Parameter]
    public required string Id { get; set; }

    private static Domain.Models.Person? _person;
    private static List<MediaPosterViewModel>? _movies;
    private bool _isSmallDevice;
    private bool _overviewExpanded;

    protected override async Task OnInitializedAsync()
    {
        _person = await mediaServerService.GetPersonAsync(Guid.Parse(Id));
        if (_person != null)
        {
            if (_person.Medias != null && _person.Medias.OfType<LiteMovie>().Any())
            {
                _movies = _person.Medias.OfType<LiteMovie>()
                    .Select(item => new MediaPosterViewModel()
                    {
                        Id = item.Id.ToString(),
                        Title = item.Title,
                        AdditionalInformations = item.ReleaseDate.HasValue ? item.ReleaseDate.Value.Year.ToString() : "",
                        PosterPictureHref = item.PosterPictureHref
                    }).ToList();
            }
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