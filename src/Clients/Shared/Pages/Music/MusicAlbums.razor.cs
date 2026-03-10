using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Requests;

namespace K7.Clients.Shared.Pages.Music;

public partial class MusicAlbums
{
    private List<AlbumViewModel> _albums = [];
    private bool _loading = true;
    private string? _searchText;

    protected override async Task OnInitializedAsync()
    {
        await LoadAlbumsAsync();
    }

    private async Task LoadAlbumsAsync()
    {
        _loading = true;

        var result = await k7ServerService.GetLiteMediasAsync(new GetMediasWithPaginationQuery
        {
            MediaTypes = [MediaType.MusicAlbum],
            PageNumber = 1,
            PageSize = 200
        });

        if (result?.Items is not null)
        {
            _albums = result.Items
                .OfType<LiteMusicAlbumDto>()
                .Where(a => string.IsNullOrEmpty(_searchText) ||
                            (a.Title?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false))
                .OrderBy(a => a.Title)
                .Select(ToViewModel)
                .ToList();
        }

        _loading = false;
    }

    private async Task OnSearchChanged(string? text)
    {
        _searchText = text;
        await LoadAlbumsAsync();
    }

    private AlbumViewModel ToViewModel(LiteMusicAlbumDto album) => new()
    {
        Id = album.Id,
        Title = album.Title,
        ReleaseYear = album.ReleaseDate,
        CoverUrl = k7ServerService.GetAbsoluteUri(
            album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)?
                .GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
    };

    private sealed record AlbumViewModel
    {
        public Guid Id { get; init; }
        public string? Title { get; init; }
        public string? ReleaseYear { get; init; }
        public string? CoverUrl { get; init; }
        public string? Artist { get; init; }
    }
}
