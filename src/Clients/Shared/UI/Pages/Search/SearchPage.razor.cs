using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Search;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Search;

public partial class SearchPage
{
    private string? _query;
    private bool _loading;
    private GlobalSearchResultDto? _result;

    [Inject] private ISearchService SearchService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;

    [SupplyParameterFromQuery(Name = "q")] public string? QueryParam { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (!string.IsNullOrWhiteSpace(QueryParam) && QueryParam != _query)
        {
            _query = QueryParam;
            await SearchAsync(_query);
        }
    }
    private async Task OnQueryDebounced(string? value)
    {
        _query = value;
        if (!string.IsNullOrWhiteSpace(_query) && _query.Length >= 2)
            await SearchAsync(_query);
        else
            _result = null;
    }

    private async Task SearchAsync(string query)
    {
        _loading = true;
        StateHasChanged();
        try
        {
            _result = await SearchService.GlobalSearchAsync(query);
        }
        catch
        {
            _result = null;
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private string GetMediaHref(LiteMediaDto media) => media switch
    {
        LiteMovieDto => $"/movies/{media.Id}",
        LiteSerieDto => $"/series/{media.Id}",
        LiteMusicAlbumDto => $"/music/albums/{media.Id}",
        LiteMusicArtistDto => $"/music/artists/{media.Id}",
        _ => "#"
    };

    private static MediaCardVariant GetVariant(MediaCardViewModel card) =>
        card.Kind == MediaCardKind.Cover ? MediaCardVariant.Cover : MediaCardVariant.Poster;

    private string? GetPersonPictureUrl(LitePersonDto person) =>
        ApiClient.GetAbsoluteUri(
            person.PortraitPicture?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;

    private string? GetCharacterPictureUrl(CharacterSearchResultDto character) =>
        ApiClient.GetAbsoluteUri(
            character.PersonPortrait?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri;

}
