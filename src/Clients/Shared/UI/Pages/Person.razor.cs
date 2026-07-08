using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.Mappings;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Components;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Entities.PersonRoles;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages;

public partial class Person : IDisposable
{
    [Parameter]
    public required string Id { get; set; }

    private PersonDto? _person;
    private const string PersonPlaceholderSrc = "_content/K7.Clients.Shared.UI/images/person-placeholder.png";
    private string? _portraitUrl;
    private string? _previousBackdropUrl;
    private readonly List<PersonBackdropSlide> _backdrops = [];
    private List<MediaCardViewModel> _medias = [];
    private List<MediaCardViewModel> _discography = [];
    private List<PersonKnownForItemDto> _knownFor = [];
    private bool _loading = true;
    private int _activeBackdropIndex;
    private Timer? _backdropTimer;
    private bool _canTrackProgress;
    private bool _canExclude;
    private bool _canSetWatchState;
    private bool _isAdmin;
    private ElementReference _scrollRoot;

    private bool HasBelowContent => _discography.Count > 0 || _medias.Count > 0 || _knownFor.Count > 0;

    private string? ActiveBackdropUrl => _backdrops.Count > 0 ? _backdrops[_activeBackdropIndex].Url : null;

    private string? ActiveDominantColor => _backdrops.Count > 0 ? _backdrops[_activeBackdropIndex].DominantColor : null;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;

    [Inject] private IUserAdminService UserAdminService { get; set; } = default!;

    [Inject] private K7HubClient K7HubClient { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
        K7HubClient.PersonPicturesUpdated += OnPersonPicturesUpdated;

        _canTrackProgress = await FeatureAccess.HasCapabilityAsync(Capability.CanResumePlayback);
        (_canExclude, _isAdmin) = await MediaCardExcludeActions.LoadPermissionsAsync(FeatureAccess);
        _canSetWatchState = await WatchStateActions.CanSetWatchStateAsync(FeatureAccess);

        _person = await k7ServerService.GetPersonAsync(Guid.Parse(Id));
        if (_person is null)
        {
            _loading = false;
            return;
        }

        _portraitUrl = apiClient.GetAbsoluteUri(
            _person.PortraitPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;

        var seenMedia = new HashSet<Guid>();
        var seenSeries = new HashSet<Guid>();

        foreach (var role in _person.Roles)
        {
            if (seenMedia.Add(role.MediaId) && _backdrops.Count < 5)
            {
                var backdropPicture = role.Media?.Pictures
                    ?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop);
                var backdropUri = backdropPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString;
                if (apiClient.GetAbsoluteUri(backdropUri)?.AbsoluteUri is { } url)
                    _backdrops.Add(new PersonBackdropSlide(url, backdropPicture?.DominantColor));
            }

            AddFilmographyRole(role, seenSeries);
        }

        var seenAlbums = new HashSet<Guid>();
        var seenAlbumTitles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in _person.Roles)
        {
            // Albums directly linked via PersonRole (e.g. MusicTrack roles)
            if (role.Media is LiteMusicAlbumDto album
                && seenAlbums.Add(album.Id)
                && (album.Title is null || seenAlbumTitles.Add(album.Title)))
            {
                AddAlbumToDiscography(album);
            }

            // Albums from MusicArtist (via MusicArtistMember role)
            if (role.Media is LiteMusicArtistDto artist)
            {
                if (artist.Albums is { } albums)
                {
                    foreach (var artistAlbum in albums)
                    {
                        if (seenAlbums.Add(artistAlbum.Id)
                            && (artistAlbum.Title is null || seenAlbumTitles.Add(artistAlbum.Title)))
                        {
                            AddAlbumToDiscography(artistAlbum);
                        }
                    }
                }

                // Albums where this artist appears as a guest (featuring)
                if (artist.GuestAppearanceAlbums is { } guestAlbums)
                {
                    foreach (var guestAlbum in guestAlbums)
                    {
                        if (seenAlbums.Add(guestAlbum.Id)
                            && (guestAlbum.Title is null || seenAlbumTitles.Add(guestAlbum.Title)))
                        {
                            AddAlbumToDiscography(guestAlbum);
                        }
                    }
                }
            }
        }

        _discography.Sort((a, b) => string.Compare(b.AdditionalInformations, a.AdditionalInformations, StringComparison.Ordinal));

        if (_backdrops.Count > 1)
        {
            _backdropTimer = new Timer(async _ =>
            {
                _previousBackdropUrl = _backdrops[_activeBackdropIndex].Url;
                _activeBackdropIndex = (_activeBackdropIndex + 1) % _backdrops.Count;
                await InvokeAsync(StateHasChanged);
            }, null, TimeSpan.FromSeconds(6), TimeSpan.FromSeconds(6));
        }

        _loading = false;

        _ = LoadKnownForAsync();
    }

    private async Task LoadKnownForAsync()
    {
        if (_person is null) return;

        try
        {
            _knownFor = await k7ServerService.GetPersonKnownForAsync(_person.Id);
            await InvokeAsync(StateHasChanged);
        }
        catch
        {
            // Non-critical
        }
    }

    private Task OpenBioDialogAsync()
    {
        if (_person is null || string.IsNullOrWhiteSpace(_person.Biography)) return Task.CompletedTask;
        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Small, FullWidth = true };
        var parameters = new K7DialogParameters();
        parameters["ContentText"] = _person.Biography;
        parameters["ButtonText"] = S["Cancel"].Value;
        return DialogService.ShowAsync<OverviewDialog>(_person.Name ?? string.Empty, parameters, options);
    }

    private void AddFilmographyRole(PersonRoleDto role, HashSet<Guid> seenSeries)
    {
        switch (role.Media)
        {
            case LiteMovieDto movie when _medias.All(m => m.Id != movie.Id.ToString()):
                if (movie.ToCardViewModel(apiClient, FormatSeasonNumber) is { } movieCard)
                    _medias.Add(movieCard);
                break;

            case LiteSerieDto serie when seenSeries.Add(serie.Id):
                if (serie.ToCardViewModel(apiClient, FormatSeasonNumber) is { } serieCard)
                    _medias.Add(serieCard);
                break;

            case LiteSerieEpisodeDto episode when seenSeries.Add(episode.SerieId):
                var serieMedia = _person!.Roles
                    .Select(r => r.Media)
                    .OfType<LiteSerieDto>()
                    .FirstOrDefault(s => s.Id == episode.SerieId);

                if (serieMedia?.ToCardViewModel(apiClient, FormatSeasonNumber) is { } serieFromRole)
                {
                    _medias.Add(serieFromRole);
                    break;
                }

                if (ToSerieCardFromEpisode(episode) is { } serieFromEpisode)
                    _medias.Add(serieFromEpisode);
                break;
        }
    }

    private MediaCardViewModel? ToSerieCardFromEpisode(LiteSerieEpisodeDto episode)
    {
        if (episode.ToCardViewModel(apiClient, FormatSeasonNumber, useParentTitle: true) is not { } card)
            return null;

        return card with
        {
            Id = episode.SerieId.ToString(),
            Kind = MediaCardKind.Serie,
            MediaType = MediaType.Serie,
            Title = episode.SerieTitle ?? card.Title,
            AdditionalInformations = episode.SerieReleaseDate?.Year.ToString(),
            ParentId = null,
            SeasonNumber = null,
            EpisodeNumber = null
        };
    }

    private string FormatSeasonNumber(int seasonNumber) => string.Format(S["SeasonNumber"], seasonNumber);

    private static string GetFilmographyHref(MediaCardViewModel item) => item.Kind switch
    {
        MediaCardKind.Serie => $"/series/{item.Id}",
        _ => $"/movies/{item.Id}"
    };

    private static MediaCardVariant GetFilmographyVariant(MediaCardViewModel item) =>
        item.Kind == MediaCardKind.Cover ? MediaCardVariant.Cover : MediaCardVariant.Poster;

    private async Task ExcludeForSelf(MediaCardViewModel item)
    {
        if (await MediaCardExcludeActions.ExcludeForSelfAsync(item, UserAdminService, Snackbar, S))
            _medias.RemoveAll(m => m.Id == item.Id || m.ParentId == item.Id);
    }

    private Task ExcludeForOthers(MediaCardViewModel item) =>
        MediaCardExcludeActions.ExcludeForOthersAsync(item, DialogService, Snackbar, S);

    private void AddAlbumToDiscography(LiteMusicAlbumDto album)
    {
        var coverUri = album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);

        if (_backdrops.Count < 5
            && apiClient.GetAbsoluteUri(coverUri?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri is { } coverUrl)
        {
            _backdrops.Add(new PersonBackdropSlide(coverUrl, coverUri?.DominantColor));
        }

        _discography.Add(new MediaCardViewModel
        {
            Id = album.Id.ToString(),
            Kind = MediaCardKind.Cover,
            MediaType = MediaType.MusicAlbum,
            UserRating = album.UserRating,
            Title = album.Title,
            AdditionalInformations = album.ReleaseDate?.Year.ToString(),
            PictureUrl = apiClient.GetAbsoluteUri(
                coverUri?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
        });
    }

    private static int Age(DateOnly birthday)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - birthday.Year;
        if (birthday.AddYears(age) > today) age--;
        return age;
    }

    private void OnPersonPicturesUpdated(Guid personId)
    {
        if (_person is null || _person.Id != personId)
            return;

        _ = InvokeAsync(ReloadPortraitAsync);
    }

    private async Task ReloadPortraitAsync()
    {
        if (!Guid.TryParse(Id, out var personId))
            return;

        var person = await k7ServerService.GetPersonAsync(personId);
        if (person is null)
            return;

        _person = person;
        var portraitUri = apiClient.GetAbsoluteUri(
            person.PortraitPicture?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri;
        _portraitUrl = MediaPictureUrlHelper.WithCacheBuster(portraitUri, DateTimeOffset.UtcNow);
        StateHasChanged();
    }

    private async Task RefreshMetadataAsync()
    {
        if (_person is null) return;
        try
        {
            await k7ServerService.RefreshPersonMetadataAsync(_person.Id);
            Snackbar.Add(L["RefreshMetadataSent"], K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
    }

    private async Task OpenEditMetadataDialogAsync()
    {
        if (_person is null) return;

        var parameters = new K7DialogParameters<EditMetadataDialog>
        {
            { x => x.Person, _person }
        };

        var options = new K7DialogOptions { CloseOnEscapeKey = true, MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true };
        var dialog = await DialogService.ShowAsync<EditMetadataDialog>(L["EditMetadata"], parameters, options);
        var result = await dialog.Result;

        if (result is { Canceled: false })
        {
            _person = await k7ServerService.GetPersonAsync(Guid.Parse(Id));
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        K7HubClient.PersonPicturesUpdated -= OnPersonPicturesUpdated;
        _backdropTimer?.Dispose();
    }

    private readonly record struct PersonBackdropSlide(string Url, string? DominantColor);
}
