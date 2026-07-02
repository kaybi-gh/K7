using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
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
    private string? _sortTitle;
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

    // ExternalIds
    private List<ExternalIdEditEntry> _externalIds = [];

    // Visibility flags
    private bool _showOriginalTitle;
    private bool _showReleaseDate;
    private string _releaseDateLabel = "";
    private bool _showOverview;
    private bool _showGenres;
    private bool _showTagline;
    private bool _showOriginalLanguage;
    private bool _showContentRating;
    private bool _showBudgetRevenue;
    private bool _showStatus;
    private bool _showNetwork;
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

    // Tabs
    private int _activeTab;
    private IReadOnlyList<TabOption<int>> _tabOptions => [
        new(0, L["TabMetadata"]),
        new(1, L["TabImages"])
    ];

    // Picture upload
    private MetadataPictureType _uploadPictureType = MetadataPictureType.Poster;
    private IBrowserFile? _pictureFile;
    private bool _isUploading;
    private List<MetadataPictureType> _allowedPictureTypes = [];

    // Provider images
    private bool _canBrowseProviderImages;
    private IReadOnlyList<ProviderImageDto>? _providerImages;
    private MetadataPictureType _providerImageFilter = MetadataPictureType.Poster;
    private ProviderImageDto? _selectedProviderImage;
    private bool _isLoadingProviderImages;
    private bool _isImportingProviderImage;

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

        _externalIds = person.ExternalIds
            .Select(e => new ExternalIdEditEntry { ProviderName = e.ProviderName, Value = e.Value })
            .ToList();

        _showBiography = true;
        _showBirthday = true;
        _showDeathday = true;
        _showBirthPlace = true;

        _canBrowseProviderImages = true;
        _allowedPictureTypes = [MetadataPictureType.Portrait];
        _uploadPictureType = MetadataPictureType.Portrait;
    }

    private void InitFromMedia(MediaDto media)
    {
        _isPersonMode = false;
        _title = media.Title;
        _sortTitle = media.SortTitle;
        _originalTitle = media.OriginalTitle;
        _releaseDateStr = media.ReleaseDate?.ToString("yyyy-MM-dd");
        _genresStr = media.Genres is not null ? string.Join(", ", media.Genres) : null;
        _lockedFields = [.. media.LockedFields ?? []];

        _externalIds = (media.ExternalIds ?? [])
            .Select(e => new ExternalIdEditEntry { ProviderName = e.ProviderName, Value = e.Value })
            .ToList();

        _showReleaseDate = true;
        _releaseDateLabel = L["ReleaseDate"].Value;

        switch (media)
        {
            case MovieDto movie:
                _showOriginalTitle = true;
                _showOverview = true;
                _showOriginalLanguage = true;
                _showContentRating = true;
                _showTagline = true;
                _showBudgetRevenue = true;
                _showGenres = true;
                _canBrowseProviderImages = true;
                _overview = movie.Overview;
                _tagline = movie.TagLine;
                _originalLanguage = movie.OriginalLanguage;
                _contentRating = movie.ContentRating;
                _budget = movie.Budget;
                _revenue = movie.Revenue;
                _allowedPictureTypes = [MetadataPictureType.Poster, MetadataPictureType.Backdrop, MetadataPictureType.Logo];
                break;

            case SerieDto serie:
                _showOriginalTitle = true;
                _showOverview = true;
                _showOriginalLanguage = true;
                _showContentRating = true;
                _showStatus = true;
                _showNetwork = true;
                _showGenres = true;
                _canBrowseProviderImages = true;
                _overview = serie.Overview;
                _originalLanguage = serie.OriginalLanguage;
                _contentRating = serie.ContentRating;
                _status = serie.Status;
                _network = serie.Network;
                _allowedPictureTypes = [MetadataPictureType.Poster, MetadataPictureType.Backdrop, MetadataPictureType.Logo];
                break;

            case SerieSeasonDto season:
                _showOverview = true;
                _canBrowseProviderImages = true;
                _overview = season.Overview;
                _allowedPictureTypes = [MetadataPictureType.Poster];
                break;

            case SerieEpisodeDto episode:
                _showOverview = true;
                _showRuntime = true;
                _canBrowseProviderImages = true;
                _overview = episode.Overview;
                _releaseDateStr = (episode.AirDate ?? media.ReleaseDate)?.ToString("yyyy-MM-dd");
                _runtime = episode.Runtime;
                _allowedPictureTypes = [MetadataPictureType.Still];
                break;

            case MusicArtistDto artist:
                _releaseDateLabel = L["FormationDate"].Value;
                _showBiography = true;
                _showCountry = true;
                _canBrowseProviderImages = true;
                _biography = artist.Biography;
                _country = artist.Country;
                _allowedPictureTypes = [MetadataPictureType.Portrait, MetadataPictureType.Cover];
                break;

            case MusicTrackDto track:
                _showGenres = true;
                _showTrackNumber = true;
                _showLyrics = true;
                _trackNumber = track.TrackNumber;
                _discNumber = track.DiscNumber;
                _lyrics = track.Lyrics;
                _allowedPictureTypes = [MetadataPictureType.Cover];
                break;

            case MusicAlbumDto:
                _showGenres = true;
                _showOverview = false;
                _canBrowseProviderImages = true;
                _allowedPictureTypes = [MetadataPictureType.Cover];
                break;
        }

        _uploadPictureType = _allowedPictureTypes.FirstOrDefault();
    }

    private string GetLockIcon(string fieldName)
    {
        return _lockedFields.Contains(fieldName) ? Phosphor.Lock : Phosphor.LockOpen;
    }

    private string GetLockClass(string fieldName)
    {
        return _lockedFields.Contains(fieldName) ? "lock-btn lock-btn--locked" : "lock-btn";
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

    private static string? ValidateDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParse(value, out _) ? null : "Invalid date format (YYYY-MM-DD)";
    }

    private static string GetPictureItemClass(MetadataPictureType type) => type switch
    {
        MetadataPictureType.Poster or MetadataPictureType.Portrait => "picture-item--poster",
        MetadataPictureType.Backdrop or MetadataPictureType.Still => "picture-item--backdrop",
        MetadataPictureType.Cover => "picture-item--cover",
        MetadataPictureType.Logo => "picture-item--logo",
        _ => "picture-item--free"
    };

    private string GetProviderGridClass() => _providerImageFilter switch
    {
        MetadataPictureType.Poster or MetadataPictureType.Portrait => "provider-images-grid--poster",
        MetadataPictureType.Backdrop or MetadataPictureType.Still => "provider-images-grid--backdrop",
        MetadataPictureType.Cover => "provider-images-grid--cover",
        _ => "provider-images-grid--free"
    };

    private async Task LoadProviderImagesAsync()
    {
        if (Media is null && Person is null)
            return;

        _isLoadingProviderImages = true;
        _providerImages = null;
        _selectedProviderImage = null;
        StateHasChanged();

        try
        {
            _providerImages = _isPersonMode && Person is not null
                ? await MediaService.GetPersonProviderImagesAsync(Person.Id)
                : await MediaService.GetMediaProviderImagesAsync(Media!.Id);

            if (_providerImages.Count > 0)
                _providerImageFilter = _providerImages[0].Type;
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _isLoadingProviderImages = false;
            StateHasChanged();
        }
    }

    private async Task ImportProviderImageAsync()
    {
        if (_selectedProviderImage is null)
            return;

        if (Media is null && Person is null)
            return;

        _isImportingProviderImage = true;
        StateHasChanged();

        try
        {
            var request = new ImportMediaPictureFromUrlRequest
            {
                Url = _selectedProviderImage.Url,
                PictureType = _selectedProviderImage.Type
            };

            if (_isPersonMode && Person is not null)
            {
                await MediaService.ImportPersonPictureFromUrlAsync(Person.Id, request);
            }
            else if (Media is not null)
            {
                await MediaService.ImportMediaPictureFromUrlAsync(Media.Id, request);
            }

            _selectedProviderImage = null;
            Snackbar.Add(L["PictureUploaded"].Value, K7Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _isImportingProviderImage = false;
            StateHasChanged();
        }
    }

    private void OnPictureFileSelected(InputFileChangeEventArgs e)
    {
        _pictureFile = e.File;
    }

    private async Task UploadPictureAsync()
    {
        if (_pictureFile is null)
            return;

        if (Media is null && Person is null)
            return;

        _isUploading = true;
        StateHasChanged();

        try
        {
            const long maxSize = 10 * 1024 * 1024;
            await using var stream = _pictureFile.OpenReadStream(maxSize);

            if (_isPersonMode && Person is not null)
            {
                await MediaService.UploadPersonPictureAsync(Person.Id, stream, _pictureFile.Name, _uploadPictureType);
            }
            else if (Media is not null)
            {
                await MediaService.UploadMediaPictureAsync(Media.Id, stream, _pictureFile.Name, _uploadPictureType);
            }

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
        try
        {
            if (_isPersonMode && Person is not null)
            {
                await MediaService.DeletePersonPictureAsync(Person.Id);
            }
            else if (Media is not null)
            {
                await MediaService.DeleteMediaPictureAsync(Media.Id, pictureId);
            }

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
            SortTitle = _sortTitle,
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
            Runtime = _runtime,
            Biography = _biography,
            Country = _country,
            TrackNumber = _trackNumber,
            DiscNumber = _discNumber,
            Lyrics = _lyrics,
            ExternalIds = _externalIds
                .Where(e => !string.IsNullOrWhiteSpace(e.ProviderName) && !string.IsNullOrWhiteSpace(e.Value))
                .Select(e => new ExternalIdEditDto { ProviderName = e.ProviderName!.Trim(), Value = e.Value!.Trim() })
                .ToList()
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
            BirthPlace = _birthPlace,
            ExternalIds = _externalIds
                .Where(e => !string.IsNullOrWhiteSpace(e.ProviderName) && !string.IsNullOrWhiteSpace(e.Value))
                .Select(e => new ExternalIdEditDto { ProviderName = e.ProviderName!.Trim(), Value = e.Value!.Trim() })
                .ToList()
        };

        await MediaService.UpdatePersonMetadataAsync(Person!.Id, request);
    }

    private static DateOnly? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParse(value, out var date) ? date : null;
    }

    private void AddExternalId()
    {
        _externalIds.Add(new ExternalIdEditEntry());
    }

    private void RemoveExternalId(ExternalIdEditEntry entry)
    {
        _externalIds.Remove(entry);
    }

    private sealed class ExternalIdEditEntry
    {
        public string? ProviderName { get; set; }
        public string? Value { get; set; }
    }
}
