using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Requests;

namespace K7.Clients.Shared.UI.Pages.Music;

public partial class MusicArtists
{
    private List<ArtistViewModel> _artists = [];
    private bool _loading = true;
    private string? _searchText;

    protected override async Task OnInitializedAsync()
    {
        await LoadArtistsAsync();
    }

    private async Task LoadArtistsAsync()
    {
        _loading = true;

        var result = await k7ServerService.GetPersonsAsync(new GetPersonsWithPaginationQuery
        {
            RoleTypes = [PersonRoleType.MusicArtist],
            PageNumber = 1,
            PageSize = 200
        });

        if (result?.Items is not null)
        {
            _artists = result.Items
                .Where(p => string.IsNullOrEmpty(_searchText) ||
                            (p.Name?.Contains(_searchText, StringComparison.OrdinalIgnoreCase) ?? false))
                .Select(ToViewModel)
                .OrderBy(a => a.Name)
                .ToList();
        }

        _loading = false;
    }

    private async Task OnSearchChanged(string? text)
    {
        _searchText = text;
        await LoadArtistsAsync();
    }

    private ArtistViewModel ToViewModel(PersonDto person) => new()
    {
        Id = person.Id,
        Name = person.Name,
        PortraitUrl = k7ServerService.GetAbsoluteUri(
            person.PortraitPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri
    };

    private sealed record ArtistViewModel
    {
        public Guid Id { get; init; }
        public string? Name { get; init; }
        public string? PortraitUrl { get; init; }
    }
}
