using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AdminHomeRowDialog
{
    private enum CatalogScope
    {
        All,
        LibraryGroups,
        Libraries
    }

    private static readonly MediaType[] _availableMediaTypes =
        [MediaType.Movie, MediaType.MusicAlbum, MediaType.Serie];

    private static readonly MediaOrderingOption[] _orderOptions =
    [
        MediaOrderingOption.CreatedDesc,
        MediaOrderingOption.CreatedAsc,
        MediaOrderingOption.TitleAsc,
        MediaOrderingOption.TitleDesc,
        MediaOrderingOption.ReleaseDateDesc,
        MediaOrderingOption.ReleaseDateAsc,
        MediaOrderingOption.LastInteractedDesc,
        MediaOrderingOption.PopularityDesc,
        MediaOrderingOption.LocalRatingDesc,
        MediaOrderingOption.PlayCountDesc
    ];

    private static readonly int[] _pageSizeOptions = [10, 20, 50, 100];

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public HomeRowEditModel? InitialModel { get; set; }
    [Parameter] public List<LibraryDto> Libraries { get; set; } = [];
    [Parameter] public List<LibraryGroupDto> LibraryGroups { get; set; } = [];

    private string _title = "";
    private HomeRowDisplayType _displayType = HomeRowDisplayType.Carousel;
    private bool _continueWatching;
    private CatalogScope _catalogScope = CatalogScope.All;
    private List<Guid> _libraryIds = [];
    private List<Guid> _libraryGroupIds = [];
    private List<MediaType> _mediaTypes = [];
    private MediaOrderingOption _orderBy = MediaOrderingOption.CreatedDesc;
    private int _pageSize = 20;

    protected override void OnParametersSet()
    {
        if (InitialModel is null)
            return;

        _title = InitialModel.Title;
        _displayType = InitialModel.DisplayType;
        _continueWatching = InitialModel.ContinueWatching;
        _libraryIds = new List<Guid>(InitialModel.LibraryIds);
        _libraryGroupIds = new List<Guid>(InitialModel.LibraryGroupIds);
        _mediaTypes = new List<MediaType>(InitialModel.MediaTypes);
        _orderBy = InitialModel.OrderBy;
        _pageSize = InitialModel.PageSize;
        _catalogScope = InferCatalogScope(InitialModel);
    }

    private static CatalogScope InferCatalogScope(HomeRowEditModel model)
    {
        if (model.LibraryGroupIds.Count > 0)
            return CatalogScope.LibraryGroups;
        if (model.LibraryIds.Count > 0)
            return CatalogScope.Libraries;
        return CatalogScope.All;
    }

    private void OnCatalogScopeChanged(CatalogScope scope)
    {
        _catalogScope = scope;
        if (_catalogScope != CatalogScope.LibraryGroups)
            _libraryGroupIds = [];
        if (_catalogScope != CatalogScope.Libraries)
            _libraryIds = [];
    }

    private void ToggleLibrary(Guid id, bool selected)
    {
        if (selected)
        {
            if (!_libraryIds.Contains(id))
                _libraryIds.Add(id);
        }
        else
        {
            _libraryIds.Remove(id);
        }
    }

    private void ToggleLibraryGroup(Guid id, bool selected)
    {
        if (selected)
        {
            if (!_libraryGroupIds.Contains(id))
                _libraryGroupIds.Add(id);
        }
        else
        {
            _libraryGroupIds.Remove(id);
        }
    }

    private void ToggleMediaType(MediaType type, bool selected)
    {
        if (selected)
        {
            if (!_mediaTypes.Contains(type))
                _mediaTypes.Add(type);
        }
        else
        {
            _mediaTypes.Remove(type);
        }
    }

    private string GetMediaTypeLabel(MediaType type) => type switch
    {
        MediaType.Movie => L["MediaTypeMovie"],
        MediaType.MusicAlbum => L["MediaTypeMusicAlbum"],
        MediaType.Serie => L["MediaTypeSerie"],
        _ => type.ToString()
    };

    private string GetCatalogScopeLabel(CatalogScope scope) => scope switch
    {
        CatalogScope.All => L["ScopeAll"],
        CatalogScope.LibraryGroups => L["ScopeLibraryGroups"],
        CatalogScope.Libraries => L["ScopeLibraries"],
        _ => scope.ToString()
    };

    private string GetOrderLabel(MediaOrderingOption option) => option switch
    {
        MediaOrderingOption.CreatedDesc => L["OrderCreatedDesc"],
        MediaOrderingOption.CreatedAsc => L["OrderCreatedAsc"],
        MediaOrderingOption.TitleAsc => L["OrderTitleAsc"],
        MediaOrderingOption.TitleDesc => L["OrderTitleDesc"],
        MediaOrderingOption.ReleaseDateDesc => L["OrderReleaseDateDesc"],
        MediaOrderingOption.ReleaseDateAsc => L["OrderReleaseDateAsc"],
        MediaOrderingOption.LastInteractedDesc => L["OrderLastInteractedDesc"],
        MediaOrderingOption.PopularityDesc => L["OrderPopularityDesc"],
        MediaOrderingOption.LocalRatingDesc => L["OrderLocalRatingDesc"],
        MediaOrderingOption.PlayCountDesc => L["OrderPlayCountDesc"],
        _ => option.ToString()
    };

    private void Cancel() => Dialog.Cancel();

    private void Submit()
    {
        var libraryIds = _continueWatching || _catalogScope != CatalogScope.Libraries
            ? []
            : new List<Guid>(_libraryIds);
        var libraryGroupIds = _continueWatching || _catalogScope != CatalogScope.LibraryGroups
            ? []
            : new List<Guid>(_libraryGroupIds);

        var model = new HomeRowEditModel
        {
            Id = InitialModel?.Id ?? Guid.NewGuid(),
            Title = _title.Trim(),
            DisplayType = _displayType,
            ContinueWatching = _continueWatching,
            LibraryIds = libraryIds,
            LibraryGroupIds = libraryGroupIds,
            MediaTypes = _continueWatching ? [] : new List<MediaType>(_mediaTypes),
            OrderBy = _orderBy,
            PageSize = _pageSize,
            IsVisible = InitialModel?.IsVisible ?? true,
            Order = InitialModel?.Order ?? 0
        };
        Dialog.Close(K7DialogResult.Ok(model));
    }
}
