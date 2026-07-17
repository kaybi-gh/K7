using K7.Clients.Shared.UI.Helpers;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

public partial class TableMediaThumb
{
    [Parameter] public string? ImageUrl { get; set; }
    [Parameter] public string? MediaType { get; set; }
    [Parameter] public TableMediaThumbHelper.Size Size { get; set; } = TableMediaThumbHelper.Size.Standard;

    private string? ResolvedImageUrl =>
        ApiClient.GetAbsoluteUri(ImageUrl)?.AbsoluteUri;

    private string ThumbStyle =>
        TableMediaThumbHelper.BuildStyle(TableMediaThumbHelper.ParseMediaType(MediaType), Size);

    private string PlaceholderIcon
    {
        get
        {
            var mediaType = TableMediaThumbHelper.ParseMediaType(MediaType);
            return mediaType is K7.Server.Domain.Enums.MediaType.SerieEpisode
                or K7.Server.Domain.Enums.MediaType.SerieSeason
                or K7.Server.Domain.Enums.MediaType.Serie
                or K7.Server.Domain.Enums.MediaType.Movie
                ? Phosphor.FilmSlate
                : Phosphor.Image;
        }
    }
}
