using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Pages;

public partial class Library
{
    [Parameter]
    public required string Id { get; set; }

    [Inject]
    private IJSRuntime JSRuntime { get; set; } = default!;

    private List<MediaCardViewModel> MediaCards { get; set; } = [];

    private bool _gridDrawerOpen = false;
    private int _spacing { get; set; } = 6;

    protected override async Task OnParametersSetAsync()
    {
        MediaCards.Clear();

        var libraryId = Guid.TryParse(Id, out var parsed) ? parsed : (Guid?)null;

        var liteMediasPage = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery()
        {
            LibraryIds = libraryId.HasValue ? [libraryId.Value] : null,
            PageNumber = 1,
            PageSize = 1000
        });

        if (liteMediasPage != null && liteMediasPage.Items?.Count != 0)
        {
            foreach (var item in liteMediasPage.Items!)
            {
                MediaCards.Add(new MediaCardViewModel()
                {
                    Id = item.Id.ToString(),
                    Title = item.Title,
                    PosterPictureHref = apiClient.GetAbsoluteUri(item.Pictures?.FirstOrDefault(x => x.Type == Server.Domain.Enums.MetadataPictureType.Poster)?.GetUri(Server.Domain.Enums.MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
                    
                });
            }
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // The top icons row is already focusable, but we can set focus to the first media poster if there are items
            // MudGrid items don't have [data-nav-row], we should let the user discover them via standard vertical movement
            await JSRuntime.InvokeVoidAsync("SpatialNavigation.focusFirst", ".media-item-link");
        }
    }
}
