using K7.Clients.Shared.Helpers;
using K7.Clients.Shared.Interfaces;
using K7.Clients.Shared.Models;
using K7.Clients.Shared.Services;
using K7.Clients.Shared.UI;
using K7.Clients.Shared.UI.Components.Dialogs;
using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Enums;
using K7.Shared.Interfaces;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Dialogs;

public partial class CustomNavItemDialog
{
    [Inject] private ILibraryService LibraryService { get; set; } = default!;
    [Inject] private IUserPreferencesService PreferencesService { get; set; } = default!;
    [Inject] private IK7DialogService DialogService { get; set; } = default!;
    [Inject] private IK7Snackbar Snackbar { get; set; } = default!;
    [Inject] private IFeatureAccessService FeatureAccess { get; set; } = default!;
    [Inject] private IDeviceService DeviceService { get; set; } = default!;

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public CustomNavItemEditModel? InitialModel { get; set; }
    [Parameter] public List<LibraryGroupDto> Groups { get; set; } = [];
    [Parameter] public IReadOnlyList<LiteCollectionDto> Collections { get; set; } = [];
    [Parameter] public IReadOnlyList<LitePlaylistDto> Playlists { get; set; } = [];

    private Guid _itemId = Guid.NewGuid();
    private CustomNavItemKind _kind = CustomNavItemKind.LibraryGroup;
    private Guid _libraryGroupId;
    private Guid _targetId;
    private int _tapChoice;
    private string _browseQuery = "";
    private string _route = CustomNavRoutes.All[0].Path;
    private string _title = "";
    private string _icon = "";
    private string _cardColor = "#283040";
    private CoverPickerResult? _pendingCover;
    private Guid? _currentCoverPictureId;
    private bool _removeCover;
    private bool _isSubmitting;
    private string? _coverDominantColor;
    private List<LibraryPictureDto> _pictures = [];
    private Guid _picturesGroupId;
    private bool _initialized;
    private bool _matchGroupAppearance = true;
    private bool _canAccessAdmin;
    private bool _isNativeClient;
    private (string GradientStart, string GradientEnd, string IconColor) _previewColors =
        LibraryGroupCardColors.ToRgba("#283040");

    private bool HasMusicLibrary =>
        Groups.Any(g => g.MediaType == LibraryMediaType.Music);

    private IEnumerable<CustomNavAppRoute> VisibleAppRoutes =>
        CustomNavRoutes.All.Where(r =>
            !r.AdminOnly
            && (CustomNavRoutes.IsAvailableForClient(r, _isNativeClient)
                || string.Equals(r.Path, _route, StringComparison.OrdinalIgnoreCase))
            && (r.Path != "/my-space/hit-parade"
                || HasMusicLibrary
                || string.Equals(r.Path, _route, StringComparison.OrdinalIgnoreCase)));

    private IEnumerable<CustomNavAppRoute> VisibleAdminRoutes =>
        CustomNavRoutes.All.Where(r => r.AdminOnly);

    private bool CanSubmit => _kind switch
    {
        CustomNavItemKind.LibraryGroup or CustomNavItemKind.LibraryBrowse => _libraryGroupId != Guid.Empty,
        CustomNavItemKind.AppRoute => CustomNavRoutes.TryNormalize(_route, out var appPath)
            && CustomNavRoutes.Find(appPath) is { AdminOnly: false },
        CustomNavItemKind.AdminRoute => _canAccessAdmin
            && CustomNavRoutes.TryNormalize(_route, out var adminPath)
            && CustomNavRoutes.Find(adminPath) is { AdminOnly: true },
        CustomNavItemKind.Collection or CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist
            => _targetId != Guid.Empty,
        _ => false
    };

    private CustomNavItemKind KindChoice
    {
        get => _kind == CustomNavItemKind.DynamicPlaylist ? CustomNavItemKind.Playlist : _kind;
        set => _kind = value;
    }

    private bool CanMatchTargetAppearance =>
        _kind is CustomNavItemKind.LibraryGroup or CustomNavItemKind.LibraryBrowse
        || (_kind == CustomNavItemKind.AppRoute && CustomNavRoutes.IsMySpacePath(_route));

    private bool UseTargetAppearance => CanMatchTargetAppearance && _matchGroupAppearance;

    private bool HasCoverOverride =>
        _pendingCover is not null || (!_removeCover && _currentCoverPictureId.HasValue);

    private CustomNavItemDto PreviewDto => new()
    {
        Id = _itemId,
        Kind = _kind,
        Title = string.IsNullOrWhiteSpace(_title) ? null : _title.Trim(),
        LibraryGroupId = _kind is CustomNavItemKind.LibraryGroup or CustomNavItemKind.LibraryBrowse
            ? _libraryGroupId
            : null,
        TargetId = _kind is CustomNavItemKind.Collection or CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist
            ? _targetId
            : null,
        Route = _kind is CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute ? _route : null,
        Icon = UseTargetAppearance || string.IsNullOrWhiteSpace(_icon) ? null : _icon,
        CardColor = UseTargetAppearance ? null : _cardColor,
        CoverPictureId = UseTargetAppearance ? null : PreviewCoverId
    };

    private string PreviewCardColor =>
        CustomNavHrefHelper.GetCardColor(PreviewDto, Groups)
        ?? CustomNavHrefHelper.GetInheritedCardColor(PreviewDto, Groups);

    private Guid? PreviewCoverId
    {
        get
        {
            if (_pendingCover?.SourcePictureId is { } pendingId)
                return pendingId;
            if (_removeCover)
                return null;
            return _currentCoverPictureId;
        }
    }

    private string PreviewTitle =>
        CustomNavHrefHelper.GetTitle(PreviewDto, Groups, key => NavL[key], Collections, Playlists);

    private string? PreviewIcon =>
        CustomNavHrefHelper.GetIcon(PreviewDto, Groups, Collections, Playlists);

    private string? PreviewImageUrl
    {
        get
        {
            var pictureId = CustomNavHrefHelper.GetCoverPictureId(PreviewDto, Groups, Collections, Playlists);
            return pictureId is { } id
                ? $"/api/metadata-pictures/{id}?size=Medium"
                : null;
        }
    }

    private string? PreviewThumbUrl
    {
        get
        {
            var pictureId = CustomNavHrefHelper.GetCoverPictureId(PreviewDto, Groups, Collections, Playlists);
            return pictureId is { } id
                ? $"/api/metadata-pictures/{id}?size=Small"
                : null;
        }
    }

    private bool CanApplyCoverDominantColor =>
        PreviewImageUrl is not null
        && DominantColorCss.ToHexColor(_coverDominantColor) is not null;

    protected override async Task OnParametersSetAsync()
    {
        if (_initialized)
            return;

        _initialized = true;
        _isNativeClient = DeviceService.GetClientType() != ClientType.Web;
        _canAccessAdmin = await FeatureAccess.HasCapabilityAsync(Capability.CanAccessAdmin);
        _libraryGroupId = Groups.FirstOrDefault()?.Id ?? Guid.Empty;
        _targetId = Collections.FirstOrDefault()?.Id ?? Playlists.FirstOrDefault()?.Id ?? Guid.Empty;

        if (InitialModel is not null)
        {
            _itemId = InitialModel.Id == Guid.Empty ? _itemId : InitialModel.Id;
            _kind = InitialModel.Kind;
            _libraryGroupId = InitialModel.LibraryGroupId ?? _libraryGroupId;
            _targetId = InitialModel.TargetId ?? _targetId;
            _tapChoice = InitialModel.TapAction switch
            {
                ExploreTapAction.Suggestions => 1,
                ExploreTapAction.Browse => 2,
                _ => 0
            };
            _browseQuery = InitialModel.BrowseQuery ?? "";
            _route = InitialModel.Route ?? CustomNavRoutes.All[0].Path;
            _title = InitialModel.Title ?? "";
            _icon = InitialModel.Icon ?? "";
            _currentCoverPictureId = InitialModel.CoverPictureId;
            _matchGroupAppearance = CanMatchTargetAppearance
                && string.IsNullOrWhiteSpace(InitialModel.Icon)
                && string.IsNullOrWhiteSpace(InitialModel.CardColor)
                && InitialModel.CoverPictureId is null;
            _cardColor = InitialModel.CardColor ?? CustomNavHrefHelper.GetInheritedCardColor(PreviewDto, Groups);
            SyncPlaylistKind();
        }
        else
        {
            _matchGroupAppearance = CanMatchTargetAppearance;
            _cardColor = CustomNavHrefHelper.GetInheritedCardColor(PreviewDto, Groups);
        }

        EnsureRouteForKind();

        RefreshPreviewTone();
        await LoadPicturesAsync();
    }

    private async Task OnKindChangedAsync()
    {
        if (!CanMatchTargetAppearance)
            _matchGroupAppearance = false;
        else if (InitialModel is null)
            _matchGroupAppearance = true;

        if (_kind == CustomNavItemKind.Collection)
            _targetId = Collections.Any(c => c.Id == _targetId) ? _targetId : Collections.FirstOrDefault()?.Id ?? Guid.Empty;
        else if (_kind is CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist)
            _targetId = Playlists.Any(p => p.Id == _targetId) ? _targetId : Playlists.FirstOrDefault()?.Id ?? Guid.Empty;

        EnsureRouteForKind();
        SyncPlaylistKind();

        if (UseTargetAppearance)
            ApplyInheritedAppearance();
        else
            RefreshPreviewTone();

        await LoadPicturesAsync();
    }

    private Task OnRouteChangedAsync()
    {
        if (!CanMatchTargetAppearance)
            _matchGroupAppearance = false;
        else if (!_matchGroupAppearance && InitialModel is null)
            _matchGroupAppearance = true;

        if (UseTargetAppearance)
            ApplyInheritedAppearance();
        else
            RefreshPreviewTone();

        return Task.CompletedTask;
    }

    private void EnsureRouteForKind()
    {
        if (_kind == CustomNavItemKind.AppRoute
            && !VisibleAppRoutes.Any(r => string.Equals(r.Path, _route, StringComparison.OrdinalIgnoreCase)))
        {
            _route = VisibleAppRoutes.FirstOrDefault()?.Path ?? "/search";
        }
        else if (_kind == CustomNavItemKind.AdminRoute
            && !VisibleAdminRoutes.Any(r => string.Equals(r.Path, _route, StringComparison.OrdinalIgnoreCase)))
        {
            _route = VisibleAdminRoutes.FirstOrDefault()?.Path ?? "/admin/dashboard";
        }
    }

    private async Task OnGroupChangedAsync()
    {
        if (UseTargetAppearance)
            ApplyInheritedAppearance();
        else
            RefreshPreviewTone();

        await LoadPicturesAsync();
    }

    private Task OnTargetChangedAsync()
    {
        SyncPlaylistKind();
        RefreshPreviewTone();
        return Task.CompletedTask;
    }

    private void SyncPlaylistKind()
    {
        if (_kind is not (CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist))
            return;

        var playlist = Playlists.FirstOrDefault(p => p.Id == _targetId);
        _kind = playlist?.IsDynamicPlaylist == true
            ? CustomNavItemKind.DynamicPlaylist
            : CustomNavItemKind.Playlist;
    }

    private string GetCollectionSelectLabel(Guid id) =>
        Collections.FirstOrDefault(c => c.Id == id)?.Title
        ?? (Collections.Count == 0 ? L["NoCollections"] : "");

    private string GetPlaylistSelectLabel(Guid id) =>
        Playlists.FirstOrDefault(p => p.Id == id)?.Title
        ?? (Playlists.Count == 0 ? L["NoPlaylists"] : "");

    private void OnMatchGroupAppearanceChanged(bool value)
    {
        _matchGroupAppearance = value;
        if (value)
            ApplyInheritedAppearance();
        else
            RefreshPreviewTone();
    }

    private void ApplyInheritedAppearance()
    {
        _icon = "";
        _pendingCover = null;
        _currentCoverPictureId = null;
        _coverDominantColor = null;
        _removeCover = true;
        _cardColor = CustomNavHrefHelper.GetInheritedCardColor(PreviewDto, Groups);
        RefreshPreviewTone();
    }

    private void RefreshPreviewTone()
    {
        var tone = CustomNavHrefHelper.GetCardTone(PreviewDto, Groups);
        _previewColors = (tone.GradientStart, _previewColors.GradientEnd, tone.IconColor);
    }

    private async Task LoadPicturesAsync()
    {
        if (_kind is not (CustomNavItemKind.LibraryGroup or CustomNavItemKind.LibraryBrowse)
            || _libraryGroupId == Guid.Empty)
        {
            _pictures = [];
            _picturesGroupId = Guid.Empty;
            return;
        }

        if (_libraryGroupId == _picturesGroupId)
            return;

        _picturesGroupId = _libraryGroupId;
        var pictures = new List<LibraryPictureDto>();
        var seen = new HashSet<Guid>();
        var group = Groups.FirstOrDefault(g => g.Id == _libraryGroupId);
        foreach (var libraryId in group?.LibraryIds ?? [])
        {
            try
            {
                var libraryPictures = await LibraryService.GetLibraryPicturesAsync(libraryId);
                foreach (var picture in libraryPictures)
                {
                    if (seen.Add(picture.Id))
                        pictures.Add(picture);
                }
            }
            catch (HttpRequestException)
            {
                break;
            }
        }

        _pictures = pictures;
    }

    private async Task OnCardColorChangedAsync(string value)
    {
        _cardColor = value;
        _previewColors = LibraryGroupCardColors.ToRgba(value);
        await InvokeAsync(StateHasChanged);
    }

    private async Task ApplyCoverDominantColorAsync()
    {
        var hex = DominantColorCss.ToHexColor(_coverDominantColor);
        if (hex is null)
            return;

        await OnCardColorChangedAsync(hex);
    }

    private async Task OpenIconPickerAsync()
    {
        var parameters = new K7DialogParameters<K7IconPickerDialog>();
        parameters.Add(x => x.InitialValue, PreviewIcon);
        parameters.Add(x => x.SearchPlaceholder, L["IconSearch"].Value);
        parameters.Add(x => x.CancelText, S["Cancel"].Value);
        parameters.Add(x => x.ConfirmText, S["Confirm"].Value);
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Small, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7IconPickerDialog>(L["IconLabel"].Value, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false })
            _icon = result.Data as string ?? "";
    }

    private async Task OpenCoverPickerAsync()
    {
        var parameters = new K7DialogParameters<K7CoverPickerDialog>();
        parameters.Add(x => x.Pictures, _pictures);
        parameters.Add(x => x.CancelText, S["Cancel"].Value);
        parameters.Add(x => x.ConfirmText, S["Confirm"].Value);
        var options = new K7DialogOptions { MaxWidth = K7DialogMaxWidth.Medium, FullWidth = true, CloseOnEscapeKey = true };
        var dialog = await DialogService.ShowAsync<K7CoverPickerDialog>(L["CoverLabel"].Value, parameters, options);
        var result = await dialog.Result;
        if (result is { Canceled: false, Data: CoverPickerResult cover })
        {
            _pendingCover = cover;
            _removeCover = false;
            _coverDominantColor = cover.SourcePictureId is { } sourceId
                ? _pictures.FirstOrDefault(p => p.Id == sourceId)?.DominantColor
                : null;
        }
    }

    private void ResetIcon() => _icon = "";

    private void ResetCover()
    {
        _pendingCover = null;
        _currentCoverPictureId = null;
        _coverDominantColor = null;
        _removeCover = true;
    }

    private void Cancel() => Dialog.Cancel();

    private async Task SubmitAsync()
    {
        if (!CanSubmit || _isSubmitting)
            return;

        _isSubmitting = true;
        try
        {
            var coverPictureId = UseTargetAppearance ? null : await ResolveCoverPictureIdAsync();
            ExploreTapAction? tap = _tapChoice switch
            {
                1 => ExploreTapAction.Suggestions,
                2 => ExploreTapAction.Browse,
                _ => null
            };

            var model = new CustomNavItemEditModel
            {
                Id = _itemId,
                Kind = _kind,
                Title = string.IsNullOrWhiteSpace(_title) ? null : _title.Trim(),
                LibraryGroupId = _kind is CustomNavItemKind.LibraryGroup or CustomNavItemKind.LibraryBrowse
                    ? _libraryGroupId
                    : null,
                TapAction = _kind == CustomNavItemKind.LibraryGroup ? tap : null,
                BrowseQuery = _kind == CustomNavItemKind.LibraryBrowse ? _browseQuery : null,
                Route = _kind is CustomNavItemKind.AppRoute or CustomNavItemKind.AdminRoute ? _route : null,
                TargetId = _kind is CustomNavItemKind.Collection or CustomNavItemKind.Playlist or CustomNavItemKind.DynamicPlaylist
                    ? _targetId
                    : null,
                Icon = UseTargetAppearance ? null : ResolveIconOverride(),
                CardColor = UseTargetAppearance ? null : ResolveCardColorOverride(),
                CoverPictureId = UseTargetAppearance ? null : coverPictureId
            };
            Dialog.Close(K7DialogResult.Ok(model));
        }
        catch (Exception ex)
        {
            Snackbar.Add(string.Format(S["ErrorWithDetails"], ex.Message), K7Severity.Error);
        }
        finally
        {
            _isSubmitting = false;
        }
    }

    private string? ResolveIconOverride()
    {
        var icon = string.IsNullOrWhiteSpace(_icon) ? null : _icon.Trim();
        if (icon is null)
            return null;

        var inherited = CustomNavHrefHelper.GetIcon(PreviewDto with { Icon = null }, Groups, Collections, Playlists);
        return string.Equals(icon, inherited, StringComparison.OrdinalIgnoreCase) ? null : icon;
    }

    private string? ResolveCardColorOverride()
    {
        var inherited = CustomNavHrefHelper.GetInheritedCardColor(PreviewDto, Groups);
        return string.Equals(_cardColor, inherited, StringComparison.OrdinalIgnoreCase)
            ? null
            : _cardColor;
    }

    private async Task<Guid?> ResolveCoverPictureIdAsync()
    {
        if (_pendingCover is null)
            return _removeCover ? null : _currentCoverPictureId;

        await using var stream = _pendingCover.File is not null
            ? _pendingCover.File.OpenReadStream(maxAllowedSize: 10 * 1024 * 1024)
            : null;

        return await PreferencesService.UploadCustomNavCoverAsync(
            _itemId,
            stream,
            _pendingCover.File?.Name,
            _pendingCover.SourcePictureId,
            _currentCoverPictureId);
    }
}
