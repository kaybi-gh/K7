using K7.Clients.Shared.Domain.Models;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.Pages;

public partial class Library
{
    [Parameter]
    public required string Id { get; set; }

    private List<MediaPosterViewModel> MediaPosterViewModels { get; set; } = [];

    private bool _gridDrawerOpen = false;
    private int _spacing { get; set; } = 6;

    protected override async Task OnInitializedAsync()
    {
        var liteMediasPage = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery()
        {
            PageNumber = 1,
            PageSize = 1000
        });

        if (liteMediasPage != null && liteMediasPage.Items?.Count != 0)
        {
            foreach (var item in liteMediasPage.Items!)
            {
                MediaPosterViewModels.Add(new MediaPosterViewModel()
                {
                    Id = item.Id.ToString(),
                    Title = item.Title,
                    PosterPictureHref = k7ServerService.GetAbsoluteUri(item.Pictures?.FirstOrDefault(x => x.Type == Server.Domain.Enums.MetadataPictureType.Poster)?.Uri?.OriginalString)?.AbsoluteUri
                    
                });
            }
        }
        base.OnInitialized();
    }
}
