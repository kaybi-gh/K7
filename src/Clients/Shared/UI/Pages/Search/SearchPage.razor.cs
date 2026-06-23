using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
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
    private List<LiteMediaDto> _movieResults = [];
    private List<LiteMediaDto> _serieResults = [];
    private List<LiteMediaDto> _musicResults = [];

    [Inject] private ISearchService SearchService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _isAdmin;
    private bool _permissionsLoaded;

    [SupplyParameterFromQuery(Name = "q")] public string? QueryParam { get; set; }

    protected override async Task OnParametersSetAsync()
    {
        if (!_permissionsLoaded)
        {
            _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
            (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);
            _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);
            _permissionsLoaded = true;
        }

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
            GroupMediaResults();
        }
        catch
        {
            _result = null;
            _movieResults = [];
            _serieResults = [];
            _musicResults = [];
        }
        finally
        {
            _loading = false;
            StateHasChanged();
        }
    }

    private void GroupMediaResults()
    {
        if (_result is null)
        {
            _movieResults = [];
            _serieResults = [];
            _musicResults = [];
            return;
        }

        _movieResults = _result.MediaResults.Where(m => m is LiteMovieDto).ToList();
        _serieResults = _result.MediaResults.Where(m => m is LiteSerieDto or LiteSerieSeasonDto or LiteSerieEpisodeDto).ToList();
        _musicResults = _result.MediaResults.Where(m => m is LiteMusicArtistDto or LiteMusicAlbumDto or LiteMusicTrackDto).ToList();
    }

    private string GetMediaHref(LiteMediaDto media) => media switch
    {
        LiteMovieDto => $"/movies/{media.Id}",
        LiteSerieDto => $"/series/{media.Id}",
        LiteSerieSeasonDto season => $"/series/{season.SerieId}/seasons/{season.SeasonNumber}",
        LiteSerieEpisodeDto episode => $"/series/{episode.SerieId}/seasons/{episode.SeasonNumber}/episodes/{episode.EpisodeNumber}",
        LiteMusicAlbumDto => $"/music/albums/{media.Id}",
        LiteMusicArtistDto => $"/music/artists/{media.Id}",
        LiteMusicTrackDto track => $"/music/albums/{track.AlbumId}",
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

    private string FormatSeasonNumber(int seasonNumber) => string.Format(S["SeasonNumber"], seasonNumber);

    private void RemoveSearchResult(MediaCardViewModel item)
    {
        _movieResults.RemoveAll(m => m.Id.ToString() == item.Id);
        _serieResults.RemoveAll(m => m.Id.ToString() == item.Id || m.Id.ToString() == item.ParentId);
        _musicResults.RemoveAll(m => m.Id.ToString() == item.Id || m.Id.ToString() == item.ParentId);
    }

    private async Task ExcludeForSelf(MediaCardViewModel item)
    {
        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))
            RemoveSearchResult(item);
    }

    private Task ExcludeForOthers(MediaCardViewModel item) =>
        MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);
}
