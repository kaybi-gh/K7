using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Medias;
using K7.Shared.Dtos.Entities.Metadatas;
using K7.Shared.Dtos.Entities.Persons;
using K7.Shared.Dtos.Requests;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class EditMetadataDialog : IDisposable
{
    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = default!;
    [Inject] private IMediaService MediaService { get; set; } = default!;
    [Inject] private IK7ServerService ApiClient { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private K7HubClient K7HubClient { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

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
    private MetadataPictureType? _uploadingPictureType;
    private List<MetadataPictureType> _allowedPictureTypes = [];

    // Provider images
    private bool _canBrowseProviderImages;
    private bool _canGenerateStillFromSource;
    private bool _isGeneratingStillFromSource;
    private CancellationTokenSource? _picturesDebounceCts;
    private IReadOnlyList<ProviderImageDto>? _providerImages;
    private MetadataPictureType _providerImageFilter = MetadataPictureType.Poster;
    private string? _importingProviderImageUrl;
    private bool _isLoadingProviderImages;
    private DateTimeOffset? _pictureCacheVersion;
    private ElementReference _dialogContentRef;

    protected override void OnInitialized()
    {
        K7HubClient.MediaMetadataRefreshed += OnMediaMetadataRefreshed;
        K7HubClient.MediaPicturesUpdated += OnMediaPicturesUpdated;
        K7HubClient.PersonPicturesUpdated += OnPersonPicturesUpdated;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
            await ResetDialogScrollAsync();
    }

    private async Task OnTabChangedAsync(int tab)
    {
        _activeTab = tab;
        await ResetDialogScrollAsync();
    }

    private async Task ResetDialogScrollAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("K7.scrollToTop", _dialogContentRef);
        }
        catch (JSDisconnectedException)
        {
        }
    }

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
    }

    private void InitFromMedia(MediaDto media)
    {
        _isPersonMode = false;
        _canGenerateStillFromSource = false;
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
                _canGenerateStillFromSource = true;
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

            case MusicAlbumDto album:
                _showGenres = true;
                _showOverview = !string.IsNullOrWhiteSpace(album.Overview);
                _overview = album.Overview;
                _canBrowseProviderImages = true;
                _allowedPictureTypes = [MetadataPictureType.Cover];
                break;
        }
    }

    private MetadataPictureDto? GetCurrentPicture(MetadataPictureType type)
    {
        if (_isPersonMode && type == MetadataPictureType.Portrait)
            return Person?.PortraitPicture;

        return Media?.Pictures?.FirstOrDefault(picture => picture.Type == type);
    }

    private void OnMediaMetadataRefreshed(Guid mediaId)
    {
        if (!IsWatchedMediaId(mediaId))
            return;

        SchedulePicturesReload();
    }

    private void OnMediaPicturesUpdated(Guid mediaId)
    {
        if (!IsWatchedMediaId(mediaId))
            return;

        SchedulePicturesReload();
    }

    private bool IsWatchedMediaId(Guid mediaId) =>
        Media switch
        {
            null => false,
            { Id: var id } when id == mediaId => true,
            SerieSeasonDto season => season.SerieId == mediaId,
            SerieEpisodeDto episode => episode.SerieId == mediaId || episode.SeasonId == mediaId,
            MusicTrackDto track => track.AlbumId == mediaId,
            _ => false
        };

    private void OnPersonPicturesUpdated(Guid personId)
    {
        if (Person?.Id != personId)
            return;

        SchedulePicturesReload();
    }

    private void SchedulePicturesReload()
    {
        _picturesDebounceCts?.Cancel();
        _picturesDebounceCts?.Dispose();
        _picturesDebounceCts = new CancellationTokenSource();
        var token = _picturesDebounceCts.Token;
        _ = DebouncedReloadPicturesAsync(token);
    }

    private async Task DebouncedReloadPicturesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(500, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
                return;

            await InvokeAsync(async () =>
            {
                await ReloadPicturesAsync();
            });
        }
        catch (OperationCanceledException)
        {
        }
    }

    private string? GetPictureDisplayUrl(MetadataPictureDto picture)
    {
        var size = MetadataPictureDisplayHelper.GetBestDisplaySize(
            picture,
            MetadataPictureSize.Small,
            MetadataPictureSize.Medium);
        var uri = ApiClient.GetAbsoluteUri(picture.GetUri(size)?.OriginalString)?.AbsoluteUri;
        var cacheVersion = _pictureCacheVersion
            ?? (Media as MediaDto)?.LastMetadataRefreshedAt;
        return MediaPictureUrlHelper.WithCacheBuster(uri, cacheVersion);
    }

    private string GetPictureRenderKey(MetadataPictureDto picture) =>
        $"{picture.Id}:{_pictureCacheVersion?.ToUnixTimeMilliseconds() ?? 0}:{string.Join('-', picture.AvailableSizes)}";

    private async Task ReloadMediaFromServerAsync()
    {
        if (Media is null)
            return;

        var fresh = await MediaService.GetMediaAsync(Media.Id, bypassCache: true);
        if (fresh is null)
            return;

        Media = fresh;
        InitFromMedia(fresh);
        _providerImages = null;
        _importingProviderImageUrl = null;
        StateHasChanged();
    }

    private async Task ReloadPersonFromServerAsync()
    {
        if (Person is null)
            return;

        var fresh = await MediaService.GetPersonAsync(Person.Id, bypassCache: true);
        if (fresh is null)
            return;

        Person = fresh;
        InitFromPerson(fresh);
        _providerImages = null;
        _importingProviderImageUrl = null;
        StateHasChanged();
    }

    private async Task ReloadPicturesAsync()
    {
        _pictureCacheVersion = DateTimeOffset.UtcNow;

        if (_isPersonMode)
            await ReloadPersonFromServerAsync();
        else
            await ReloadMediaFromServerAsync();
    }

    public void Dispose()
    {
        K7HubClient.MediaMetadataRefreshed -= OnMediaMetadataRefreshed;
        K7HubClient.MediaPicturesUpdated -= OnMediaPicturesUpdated;
        K7HubClient.PersonPicturesUpdated -= OnPersonPicturesUpdated;
        _picturesDebounceCts?.Cancel();
        _picturesDebounceCts?.Dispose();
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
            _lockedFields.Add(fieldName);

        StateHasChanged();
    }

    private async Task ToggleAllPicturesLockAsync()
    {
        if (IsAllPicturesLocked())
        {
            _lockedFields.Remove("Pictures");
        }
        else
        {
            _lockedFields.Add("Pictures");
            _lockedFields.RemoveWhere(field => field.StartsWith("Pictures:", StringComparison.Ordinal));
        }

        StateHasChanged();
        await SaveLockedFieldsAsync();
    }

    private async Task TogglePictureTypeLockAsync(MetadataPictureType type)
    {
        if (IsAllPicturesLocked())
        {
            _lockedFields.Remove("Pictures");
            foreach (var allowedType in _allowedPictureTypes)
            {
                if (allowedType != type)
                    _lockedFields.Add(GetPictureTypeLockField(allowedType));
            }
        }
        else
        {
            var fieldName = GetPictureTypeLockField(type);
            if (!_lockedFields.Remove(fieldName))
                _lockedFields.Add(fieldName);
        }

        StateHasChanged();
        await SaveLockedFieldsAsync();
    }

    private static string GetPictureTypeLockField(MetadataPictureType type) => $"Pictures:{type}";

    private bool IsAllPicturesLocked() => _lockedFields.Contains("Pictures");

    private bool IsFieldLocked(string fieldName) => _lockedFields.Contains(fieldName);

    private string GetFieldClass(string fieldName, string? baseClass = null) =>
        string.Join(" ", new[] { baseClass, IsFieldLocked(fieldName) ? "metadata-field--locked" : null }
            .Where(static s => !string.IsNullOrWhiteSpace(s)));

    private bool IsExternalIdsLocked() => IsFieldLocked("ExternalIds");

    private bool IsPictureTypeLocked(MetadataPictureType type) =>
        IsAllPicturesLocked() || _lockedFields.Contains(GetPictureTypeLockField(type));

    private string GetPictureTypeLockIcon(MetadataPictureType type) =>
        IsPictureTypeLocked(type) ? Phosphor.Lock : Phosphor.LockOpen;

    private string GetPictureTypeLockClass(MetadataPictureType type) =>
        IsPictureTypeLocked(type) ? "lock-btn lock-btn--locked" : "lock-btn";

    private string GetPictureTypeLockTooltip(MetadataPictureType type) =>
        IsPictureTypeLocked(type) ? L["UnlockField"].Value : L["LockField"].Value;

    private async Task SaveLockedFieldsAsync()
    {
        try
        {
            if (_isPersonMode && Person is not null)
            {
                await MediaService.UpdatePersonMetadataAsync(Person.Id, new UpdatePersonMetadataRequest
                {
                    LockedFields = [.. _lockedFields]
                });
            }
            else if (Media is not null)
            {
                await MediaService.UpdateMediaMetadataAsync(Media.Id, new UpdateMediaMetadataRequest
                {
                    LockedFields = [.. _lockedFields]
                });
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
    }

    private void CloseImagesTab() => Dialog.Close(K7DialogResult.Ok());

    private void OnOriginalLanguageChanged(string? value)
    {
        if (IsFieldLocked("OriginalLanguage"))
            return;

        _originalLanguage = value;
    }

    private void OnReleaseDateChanged(string? value)
    {
        if (IsFieldLocked("ReleaseDate"))
            return;

        _releaseDateStr = value;
    }

    private void OnBirthdayChanged(string? value)
    {
        if (IsFieldLocked("Birthday"))
            return;

        _birthdayStr = value;
    }

    private void OnDeathdayChanged(string? value)
    {
        if (IsFieldLocked("Deathday"))
            return;

        _deathdayStr = value;
    }

    private static string? ValidateDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return DateOnly.TryParse(value, out _) ? null : "Invalid date format (YYYY-MM-DD)";
    }

    private static string GetPictureSlotPreviewClass(MetadataPictureType type) => type switch
    {
        MetadataPictureType.Poster or MetadataPictureType.Portrait => "picture-slot-preview--poster",
        MetadataPictureType.Backdrop or MetadataPictureType.Still => "picture-slot-preview--backdrop",
        MetadataPictureType.Cover => "picture-slot-preview--cover",
        MetadataPictureType.Logo => "picture-slot-preview--logo",
        _ => "picture-slot-preview--free"
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
        _importingProviderImageUrl = null;
        StateHasChanged();

        try
        {
            _providerImages = _isPersonMode && Person is not null
                ? await MediaService.GetPersonProviderImagesAsync(Person.Id)
                : await MediaService.GetMediaProviderImagesAsync(Media!.Id);

            if (_providerImages.Count > 0)
            {
                var matchedType = _allowedPictureTypes
                    .FirstOrDefault(type => _providerImages.Any(image => image.Type == type));
                _providerImageFilter = _providerImages.Any(image => image.Type == matchedType)
                    ? matchedType
                    : _providerImages[0].Type;
            }
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

    private async Task ImportProviderImageAsync(ProviderImageDto image)
    {
        if (Media is null && Person is null)
            return;

        _importingProviderImageUrl = image.Url;
        StateHasChanged();

        try
        {
            var request = new ImportMediaPictureFromUrlRequest
            {
                Url = image.Url,
                PictureType = image.Type
            };

            if (_isPersonMode && Person is not null)
            {
                await MediaService.ImportPersonPictureFromUrlAsync(Person.Id, request);
            }
            else if (Media is not null)
            {
                await MediaService.ImportMediaPictureFromUrlAsync(Media.Id, request);
            }

            Snackbar.Add(L["PictureUploaded"].Value, K7Severity.Success);
            await ReloadPicturesAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _importingProviderImageUrl = null;
            StateHasChanged();
        }
    }

    private async Task OnSlotFileSelectedAsync(MetadataPictureType type, InputFileChangeEventArgs e)
    {
        if (e.File is null)
            return;

        _uploadingPictureType = type;
        StateHasChanged();

        try
        {
            await UploadPictureAsync(e.File, type);
        }
        finally
        {
            _uploadingPictureType = null;
            StateHasChanged();
        }
    }

    private async Task UploadPictureAsync(IBrowserFile file, MetadataPictureType pictureType)
    {
        if (Media is null && Person is null)
            return;

        try
        {
            const long maxSize = 10 * 1024 * 1024;
            await using var stream = file.OpenReadStream(maxSize);

            if (_isPersonMode && Person is not null)
            {
                await MediaService.UploadPersonPictureAsync(Person.Id, stream, file.Name, pictureType);
            }
            else if (Media is not null)
            {
                await MediaService.UploadMediaPictureAsync(Media.Id, stream, file.Name, pictureType);
            }

            Snackbar.Add(L["PictureUploaded"].Value, K7Severity.Success);
            await ReloadPicturesAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
    }

    private async Task GenerateStillFromSourceAsync()
    {
        if (Media is null || !_canGenerateStillFromSource)
            return;

        _isGeneratingStillFromSource = true;
        StateHasChanged();

        try
        {
            var pictureId = await MediaService.GenerateEpisodeStillFromSourceAsync(Media.Id);
            Snackbar.Add(L["GenerateStillFromSourceSuccess"].Value, K7Severity.Success);
            await ReloadPicturesAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add(ex.Message, K7Severity.Error);
        }
        finally
        {
            _isGeneratingStillFromSource = false;
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
            await ReloadPicturesAsync();
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

    private void RemoveExternalId(ExternalIdEditEntry entry)
    {
        if (IsExternalIdsLocked())
            return;

        _externalIds.Remove(entry);
    }

    private void AddExternalId()
    {
        if (IsExternalIdsLocked())
            return;

        _externalIds.Add(new ExternalIdEditEntry());
    }

    private sealed class ExternalIdEditEntry
    {
        public string? ProviderName { get; set; }
        public string? Value { get; set; }
    }
}
