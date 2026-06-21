using K7.Clients.Shared.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components.Explore;

public partial class ExploreLibraryGroupView
{
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private NavigationManager NavigationManager { get; set; } = default!;

    [Parameter, EditorRequired] public LibraryGroupDto Group { get; set; } = default!;
    [Parameter] public bool IsTv { get; set; }

    private List<MediaGenreDto> _genres = [];
    private Guid[] _libraryGroupIds = [];
    private Guid[] _libraryIds = [];
    private bool _loading = true;
    private Guid _loadedGroupId;

    private string BrowseHref => $"/library-groups/{Group.Id}";

    private static readonly HashSet<MediaType> _movieTypes = [MediaType.Movie];
    private static readonly HashSet<MediaType> _serieTypes = [MediaType.Serie];
    private static readonly HashSet<MediaType> _musicAlbumTypes = [MediaType.MusicAlbum];

    private static readonly HashSet<MediaOrderingOption> CreatedOrder = [MediaOrderingOption.CreatedDesc];
    private static readonly HashSet<MediaOrderingOption> TrendingOrder = [MediaOrderingOption.TrendingDesc];
    private static readonly HashSet<MediaOrderingOption> ProviderRatingOrder = [MediaOrderingOption.ProviderRatingDesc];
    private static readonly HashSet<GenreOrderingOption> GenreOrder = [GenreOrderingOption.UserPlayCountDesc, GenreOrderingOption.MediaCountDesc];

    protected override async Task OnParametersSetAsync()
    {
        if (Group.Id == _loadedGroupId)
            return;

        _loadedGroupId = Group.Id;
        _loading = true;
        _genres = [];
        _libraryGroupIds = [Group.Id];
        _libraryIds = Group.LibraryIds.ToArray();

        if (Group.MediaType is LibraryMediaType.Movie or LibraryMediaType.Serie)
            _genres = await LoadGenresAsync(Group.MediaType);

        _loading = false;
    }

    private void GoBack() => NavigationManager.NavigateTo("/explore");

    private async Task<List<MediaGenreDto>> LoadGenresAsync(LibraryMediaType mediaType)
    {
        var mediaTypes = mediaType switch
        {
            LibraryMediaType.Movie => _movieTypes,
            LibraryMediaType.Serie => _serieTypes,
            _ => null
        };

        if (mediaTypes is null)
            return [];

        try
        {
            var result = await MediaService.GetMediaGenresAsync(new GetMediaGenresQuery
            {
                LibraryGroupIds = _libraryGroupIds,
                MediaTypes = mediaTypes,
                OrderBy = GenreOrder,
                PageNumber = 1,
                PageSize = 3
            });

            return result?.Items?.ToList() ?? [];
        }
        catch
        {
            return [];
        }
    }
}
