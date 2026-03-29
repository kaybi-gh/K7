using K7.Clients.Shared.Models;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class Library
{
    [Inject] private NavigationManager Navigation { get; set; } = default!;

    [Parameter]
    public required string Id { get; set; }

    private List<MediaCardViewModel> MediaCards { get; set; } = [];
    private bool _loading = true;

    protected override async Task OnParametersSetAsync()
    {
        _loading = true;
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
                    PictureUrl = apiClient.GetAbsoluteUri(item.Pictures?.FirstOrDefault(x => x.Type == Server.Domain.Enums.MetadataPictureType.Poster)?.GetUri(Server.Domain.Enums.MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
                });
            }
        }

        _loading = false;
    }

    private void NavigateToItem(MediaCardViewModel item)
    {
        Navigation.NavigateTo($"/movies/{item.Id}");
    }
}
