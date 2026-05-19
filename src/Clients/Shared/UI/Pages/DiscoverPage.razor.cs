using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages;

public partial class DiscoverPage
{
    private List<MediaCardViewModel> _items = [];
    private bool _loading = true;

    protected override async Task OnInitializedAsync()
    {
        var query = new GetHomeFeedQuery
        {
            OrderBy = [MediaOrderingOption.RecommendedForYou],
            PageNumber = 1,
            PageSize = 40
        };

        var result = await MediaService.GetHomeFeedAsync(query);
        if (result?.Items is not null)
        {
            _items = result.Items.Select(item => new MediaCardViewModel
            {
                Id = item.Id.ToString(),
                Title = item.Title,
                Kind = item.MediaType switch
                {
                    MediaType.Serie => MediaCardKind.Serie,
                    _ => MediaCardKind.Poster
                },
                PictureUrl = ApiClient.GetAbsoluteUri(
                    item.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                        ?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
            }).ToList();
        }

        _loading = false;
    }

    private static string GetHref(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Serie => $"/series/{item.Id}",
        _ => $"/movies/{item.Id}"
    };
}
