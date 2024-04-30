using MediaClient.Shared.Domain.Models;
using Microsoft.AspNetCore.Components;

namespace MediaClient.Shared.Pages;

public partial class Library
{
    [Parameter]
    public required string Id { get; set; }

    private List<MediaItem> MediaItems { get; set; } = [];

    protected override async Task OnInitializedAsync()
    {
        var test = await mediaServerService.GetMediasAsync(new GetMediasWithPaginationQuery()
        {
            PageNumber = 1,
            PageSize = 1000
        });

        if (test != null && test.Items.Count != 0)
        {
            foreach (var item in test.Items)
            {
                MediaItems.Add(new MediaItem()
                {
                    Id = item.Id.ToString(),
                    PosterPicture = $"{mediaServerService.GetBaseUrl()}{item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?.Uri?.OriginalString}"
                });
            }
        }
        base.OnInitialized();
    }
}