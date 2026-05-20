using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;
using K7.Clients.Shared.UI.Components.Dialogs;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace K7.Clients.Shared.UI.Pages;

public partial class Person : IDisposable
{
    [Parameter]
    public required string Id { get; set; }

    private PersonDto? _person;
    private string? _portraitUrl;
    private List<string> _backdropUrls = [];
    private List<MediaCardViewModel> _medias = [];
    private List<MediaCardViewModel> _discography = [];
    private List<PersonKnownForItemDto> _knownFor = [];
    private bool _loading = true;
    private int _activeBackdropIndex;
    private Timer? _backdropTimer;

    [Inject] private IK7DialogService DialogService { get; set; } = default!;

    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    protected override async Task OnInitializedAsync()
    {
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
            if (seenMedia.Add(role.MediaId) && _backdropUrls.Count < 5)
            {
                var backdropUri = role.Media?.Pictures
                    ?.FirstOrDefault(p => p.Type == MetadataPictureType.Backdrop)
                    ?.GetUri(MetadataPictureSize.Medium)?.OriginalString;
                if (apiClient.GetAbsoluteUri(backdropUri)?.AbsoluteUri is { } url)
                    _backdropUrls.Add(url);
            }

            switch (role.Media)
            {
                case LiteMovieDto movie when _medias.All(m => m.Id != movie.Id.ToString()):
                    _medias.Add(new MediaCardViewModel
                    {
                        Id = movie.Id.ToString(),
                        Title = movie.Title,
                        AdditionalInformations = movie.ReleaseDate?.Year.ToString(),
                        PictureUrl = apiClient.GetAbsoluteUri(
                            movie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                                ?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
                    });
                    break;

                case LiteSerieDto serie when seenSeries.Add(serie.Id):
                    _medias.Add(new MediaCardViewModel
                    {
                        Id = serie.Id.ToString(),
                        Kind = MediaCardKind.Serie,
                        Title = serie.Title,
                        AdditionalInformations = serie.ReleaseDate?.Year.ToString(),
                        PictureUrl = apiClient.GetAbsoluteUri(
                            serie.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                                ?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
                    });
                    break;

                case LiteSerieEpisodeDto episode when seenSeries.Add(episode.SerieId):
                    _medias.Add(new MediaCardViewModel
                    {
                        Id = episode.SerieId.ToString(),
                        Kind = MediaCardKind.Serie,
                        Title = episode.SerieTitle,
                        PictureUrl = apiClient.GetAbsoluteUri(
                            episode.SeriePictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster)
                                ?.GetUri(MetadataPictureSize.Small)?.OriginalString)?.AbsoluteUri
                    });
                    break;
            }
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

        if (_backdropUrls.Count > 1)
        {
            _backdropTimer = new Timer(async _ =>
            {
                _activeBackdropIndex = (_activeBackdropIndex + 1) % _backdropUrls.Count;
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

    private void AddAlbumToDiscography(LiteMusicAlbumDto album)
    {
        var coverUri = album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Cover)
            ?? album.Pictures?.FirstOrDefault(p => p.Type == MetadataPictureType.Poster);

        if (_backdropUrls.Count < 5
            && apiClient.GetAbsoluteUri(coverUri?.GetUri(MetadataPictureSize.Medium)?.OriginalString)?.AbsoluteUri is { } coverUrl)
        {
            _backdropUrls.Add(coverUrl);
        }

        _discography.Add(new MediaCardViewModel
        {
            Id = album.Id.ToString(),
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

    public void Dispose() => _backdropTimer?.Dispose();
}
