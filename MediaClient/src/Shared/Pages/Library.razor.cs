using MediaClient.Shared.Domain.Models;
using Microsoft.AspNetCore.Components;

namespace MediaClient.Shared.Pages;

public partial class Library
{
    [Parameter]
    public required string Id { get; set; }

    private List<MediaPosterViewModel> MediaPosterViewModels { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        var liteMediasPage = await mediaServerService.GetLiteMediasAsync(new GetLiteMediasQuery()
        {
            PageNumber = 1,
            PageSize = 1000
        });

        if (liteMediasPage != null && liteMediasPage.Items.Count != 0)
        {
            foreach (var item in liteMediasPage.Items)
            {
                MediaPosterViewModels.Add(new MediaPosterViewModel()
                {
                    Id = item.Id.ToString(),
                    Title = item.Title,
                    PosterPictureHref = item.PosterPictureHref
                });
            }
        }
        base.OnInitialized();
    }
}