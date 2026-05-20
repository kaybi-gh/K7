using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Requests;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditMetadataDialog
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;

    [Parameter] public MediaDto? Media { get; set; }
    [Parameter] public PersonDto? Person { get; set; }

    private HashSet<string> _lockedFields = [];
    private bool _isSubmitting;

    // Common fields
    private string? _title;
    private string? _originalTitle;
    private string? _releaseDateStr;
    private string? _overview;
    private string? _genresStr;

    // Movie-specific
    private string? _tagline;
    private string? _originalLanguage;
    private string? _contentRating;
    private long? _budget;
    private long? _revenue;

    // Serie-specific
    private string? _status;
    private string? _network;

    // Episode-specific
    private string? _airDateStr;
    private int? _runtime;

    // MusicArtist-specific
    private string? _biography;
    private string? _country;

    // MusicTrack-specific
    private int? _trackNumber;
    private int? _discNumber;
    private string? _lyrics;

    // Person-specific
    private string? _birthdayStr;
    private string? _deathdayStr;
    private string? _birthPlace;

    // Visibility flags
    private bool _showOriginalTitle;
    private bool _showReleaseDate;
    private bool _showOverview;
    private bool _showGenres;
    private bool _showTagline;
    private bool _showOriginalLanguage;
    private bool _showContentRating;
    private bool _showBudgetRevenue;
    private bool _showStatus;
    private bool _showNetwork;
    private bool _showAirDate;
    private bool _showRuntime;
    private bool _showBiography;
    private bool _showCountry;
    private bool _showTrackNumber;
    private bool _showLyrics;

    // Person mode
    private bool _isPersonMode;
    private bool _showBirthday;
    private bool _showDeathday;
    private bool _showBirthPlace;

    // Picture upload
    private MetadataPictureType _uploadPictureType = MetadataPictureType.Poster;
    private IBrowserFile? _pictureFile;
    private bool _isUploading;

    protected override void OnParametersSet()
    {
        if (Person is not null)
        {
            InitFromPerson(Person);
        }
        else if (Media is not null)
        {
            InitFromMedia(Media);
        }
    }

    private void InitFromPerson(PersonDto person)
    {
        _isPersonMode = true;
        _title = person.Name;
        _biography = person.Biography;
        _birthdayStr = person.Birthday?.ToString("yyyy-MM-dd");
        _deathdayStr = person.Deathday?.ToString("yyyy-MM-dd");
        _birthPlace = person.BirthPlace;
        _lockedFields = [.. person.LockedFields];

        _showBiography = true;
        _showBirthday = true;
        _showDeathday = true;
        _showBirthPlace = true;
    }

    private void InitFromMedia(MediaDto media)
    {
        _isPersonMode = false;
        _title = media.Title;
        _releaseDateStr = media.ReleaseDate?.ToString("yyyy-MM-dd");
        _genresStr = media.Genres is not null ? string.Join(", ", media.Genres) : null;
        _lockedFields = [.. media.LockedFields ?? []];

        _showReleaseDate = true;
        _showGenres = true;

        switch (media)
        {
            case MovieDto movie:
                _showOriginalTitle = true;
                _showOverview = true;
                _showOriginalLanguage = true;
                _showContentRating = true;
                _showTagline = true;
                _showBudgetRevenue = true;
                _overview = movie.Overview;
                _tagline = movie.TagLine;
                _originalLanguage = movie.OriginalLanguage;
                _contentRating = movie.ContentRating;
                _budget = movie.Budget;
                _revenue = movie.Revenue;
                break;

            case SerieDto serie:
                _showOriginalTitle = true;
                _showOverview = true;
                _showOriginalLanguage = true;
                _showContentRating = true;
                _showStatus = true;
                _showNetwork = true;
                _overview = serie.Overview;
                _originalLanguage = serie.OriginalLanguage;
                _contentRating = serie.ContentRating;
                _status = serie.Status;
                _network = serie.Network;
                break;

            case SerieEpisodeDto episode:
                _showOverview = true;
                _showAirDate = true;
                _showRuntime = true;
                _overview = episode.Overview;
                _airDateStr = episode.AirDate?.ToString("yyyy-MM-dd");
                _runtime = episode.Runtime;
                break;

            case MusicArtistDto artist:
                _showBiography = true;
                _showCountry = true;
                _biography = artist.Biography;
                _country = artist.Country;
                break;

            case MusicTrackDto track:
                _showTrackNumber = true;
                _showLyrics = true;
                _trackNumber = track.TrackNumber;
                _discNumber = track.DiscNumber;
                _lyrics = track.Lyrics;
                break;

            case MusicAlbumDto:
                _showOverview = false;
                break;
        }
    }

    private string GetLockIcon(string fieldName)
    {
        return _lockedFields.Contains(fieldName) ? Phosphor.Lock : Phosphor.LockOpen;
    }

    private string GetLockTooltip(string fieldName)
    {
        return _lockedFields.Contains(fieldName) ? L["UnlockField"].Value : L["LockField"].Value;
    }

    private void ToggleLock(string fieldName)
    {
        if (!_lockedFields.Remove(fieldName))
        {
            _lockedFields.Add(fieldName);
        }
    }

    private void OnReleaseDateChanged(string? value) => _releaseDateStr = value;
    private void OnAirDateChanged(string? value) => _airDateStr = value;

    private void OnPictureFileSelected(InputFileChangeEventArgs e)
    {
        _pictureFile = e.File;
    }

    private async Task UploadPictureAsync()
    {
        if (_pictureFile is null || Media is null)
            return;

        _isUploading = true;
        StateHasChanged();

        try
        {
            const long maxSize = 10 * 1024 * 1024;
            await using var stream = _pictureFile.OpenReadStream(maxSize);
            await MediaService.UploadMediaPictureAsync(Media.Id, stream, _pictureFile.Name, _uploadPictureType);
            _pictureFile = null;
            Snackbar.Add(L["PictureUploaded"].Value, K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _isUploading = false;
            StateHasChanged();
        }
    }

    private async Task DeletePictureAsync(Guid pictureId)
    {
        if (Media is null)
            return;

        try
        {
            await MediaService.DeleteMediaPictureAsync(Media.Id, pictureId);
            Snackbar.Add(L["PictureDeleted"].Value, K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
    }

    private void Cancel() => Dialog.Cancel();

    private async Task Submit()
    {
        _isSubmitting = true;
        StateHasChanged();

        try
        {
            if (_isPersonMode && Person is not null)
            {
                await SubmitPersonAsync();
            }
            else if (Media is not null)
            {
                await SubmitMediaAsync();
            }

            Dialog.Close(K7DialogResult.Ok());
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
            StateHasChanged();
        }
    }

    private async Task SubmitMediaAsync()
    {
        var request = new UpdateMediaMetadataRequest
        {
            LockedFields = [.. _lockedFields],
            Title = _title,
            OriginalTitle = _originalTitle,
            ReleaseDate = ParseDate(_releaseDateStr),
            Genres = _genresStr?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList(),
            Tagline = _tagline,
            Overview = _overview,
            OriginalLanguage = _originalLanguage,
            ContentRating = _contentRating,
            Budget = _budget,
            Revenue = _revenue,
            Status = _status,
            Network = _network,
            AirDate = ParseDate(_airDateStr),
            Runtime = _runtime,
            Biography = _biography,
            Country = _country,
            TrackNumber = _trackNumber,
            DiscNumber = _discNumber,
            Lyrics = _lyrics
        };

        await MediaService.UpdateMediaMetadataAsync(Media!.Id, request);
    }

    private async Task SubmitPersonAsync()
    {
        var request = new UpdatePersonMetadataRequest
        {
            LockedFields = [.. _lockedFields],
            Name = _title,
            Biography = _biography,
            Birthday = ParseDate(_birthdayStr),
            Deathday = ParseDate(_deathdayStr),
            BirthPlace = _birthPlace
        };

        await MediaService.UpdatePersonMetadataAsync(Person!.Id, request);
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParse(value, out var date) ? date : null;
    }
}
