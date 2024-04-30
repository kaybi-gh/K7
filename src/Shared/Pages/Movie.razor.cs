using MediaClient.Shared.Domain.Models;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace MediaClient.Shared.Pages;

public partial class Movie
{
    [Parameter]
    public required string Id { get; set; }

    private static MediaItem? _movie;
    private static List<PersonRole> _casting = [];
    private bool _isSmallDevice;
    private bool _overviewExpanded;

    protected override async Task OnInitializedAsync()
    {
        var mediaDto = await mediaServerService.GetMediaAsync(Guid.Parse(Id));

        if (mediaDto != null)
        {
            _movie = new MediaItem()
            {
                Id = mediaDto.Id.ToString(),
                Title = mediaDto.Title,
                Synopsis = ((MovieDto)mediaDto).Overview,
                AdditionalInformations = mediaDto.ReleaseDate.Value.Year.ToString(),
                PosterPicture = $"{mediaServerService.GetBaseUrl()}{mediaDto.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?.Uri?.OriginalString}"
            };

            _casting = mediaDto.PersonRoles.Select(x => new PersonRole()
            {
                Id = x.Id.ToString(),
                PersonId = x.PersonId,
                PersonSlug = x.PersonSlug,
                PersonName = x.PersonName,
                CharacterName = ((ActorDto)x).CharacterName,
                PortraitPicture = $"{mediaServerService.GetBaseUrl()}{x.PortraitPicture?.Uri?.OriginalString}"
            }).ToList();
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