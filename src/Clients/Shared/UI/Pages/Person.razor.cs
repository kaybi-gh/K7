using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace K7.Clients.Shared.UI.Pages;

public partial class Person
{
    [Parameter]
    public required string Id { get; set; }

    private static PersonDto? _person;
    private static List<MediaPosterViewModel>? _movies;
    private bool _isSmallDevice;
    private bool _overviewExpanded;

    protected override async Task OnInitializedAsync()
    {
        _person = await k7ServerService.GetPersonAsync(Guid.Parse(Id));
        if (_person != null)
        {
            var personMedias = _person.Roles.Select(x => x.Media);
            if (personMedias != null && personMedias.OfType<LiteMovieDto>().Any())
            {
                _movies = personMedias.OfType<LiteMovieDto>()
                    .Select(item => new MediaPosterViewModel()
                    {
                        Id = item.Id.ToString(),
                        Title = item.Title,
                        AdditionalInformations = item.ReleaseDate,
                        PosterPictureHref = k7ServerService.GetAbsoluteUri(item.Pictures?.FirstOrDefault(x => x.Type == Server.Domain.Enums.MetadataPictureType.Poster)?.GetUri(Server.Domain.Enums.MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
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
