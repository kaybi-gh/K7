using K7.Clients.Shared.Mappings;
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
                if (item.ToCardViewModel(apiClient) is { } vm)
                    MediaCards.Add(vm);
            }
        }

        _loading = false;
    }

    private void NavigateToItem(MediaCardViewModel item)
    {
        Navigation.NavigateTo(GetItemHref(item));
    }

    private static string GetItemHref(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Cover => $"/music/albums/{item.Id}",
        MediaCardKind.Serie => $"/series/{item.Id}",
        _ => $"/movies/{item.Id}"
    };
}
