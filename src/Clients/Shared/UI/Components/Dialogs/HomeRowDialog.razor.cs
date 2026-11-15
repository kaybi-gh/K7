using K7.Clients.Shared.Models;
using K7.Clients.Shared.UI.Helpers;
using K7.Clients.Shared.UI.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Localization;

namespace K7.Clients.Shared.UI.Components.Dialogs;

public partial class HomeRowDialog
{
    /// <summary>The two families of rows: cards side by side, or one title at a time.</summary>
    private enum RowKind
    {
        Carousel,
        Banner
    }

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
        MediaOrderingOption.PlayCountDesc,
        MediaOrderingOption.RecommendedForYou
    ];

    private static readonly int[] _autoAdvanceOptions = [0, 5, 8, 12, 20];

    private static readonly int[] _pageSizeOptions = [10, 20, 50, 100];

    // A banner shows one title at a time: a handful is plenty.
    private static readonly int[] _bannerPageSizeOptions = [3, 5, 10, 15];

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Inject] private IStringLocalizer<Home> HomeL { get; set; } = default!;

    [Parameter] public HomeRowEditModel? InitialModel { get; set; }
    [Parameter] public List<LibraryDto> Libraries { get; set; } = [];
    [Parameter] public List<LibraryGroupDto> LibraryGroups { get; set; } = [];

    /// <summary>Title of the row that already is the banner of this home, if another row is. A home has one banner.</summary>
    [Parameter] public string? ExistingBannerTitle { get; set; }

    private string _title = "";
    private string? _titleKey;
    private string? _translatedTitle;
    private HomeRowDisplayType _displayType = HomeRowDisplayType.Carousel;
    private bool _continueWatching;
    private CatalogScope _catalogScope = CatalogScope.All;
    private List<Guid> _libraryIds = [];
    private List<Guid> _libraryGroupIds = [];
    private List<MediaType> _mediaTypes = [];
    private MediaOrderingOption _orderBy = MediaOrderingOption.CreatedDesc;
    private int _pageSize = 20;

    // 0 stands for "off" in the select.
    private int _autoAdvanceSeconds;

    protected override void OnParametersSet()
    {
        if (InitialModel is null)
            return;

        // A ready-made row stores a key the app translates. Show the translation, and keep the key
        // unless the title is really changed, so the row still follows the language.
        _titleKey = InitialModel.Title;
        _translatedTitle = HomeLayoutRowTitleHelper.Localize(HomeL, InitialModel.Title);
        _title = _translatedTitle;
        _displayType = InitialModel.DisplayType;
        _continueWatching = InitialModel.ContinueWatching;
        _libraryIds = new List<Guid>(InitialModel.LibraryIds);
        _libraryGroupIds = new List<Guid>(InitialModel.LibraryGroupIds);
        _mediaTypes = new List<MediaType>(InitialModel.MediaTypes);
        _orderBy = InitialModel.OrderBy;
        _pageSize = InitialModel.PageSize;
        _autoAdvanceSeconds = InitialModel.AutoAdvanceSeconds ?? 0;
        _catalogScope = InferCatalogScope(InitialModel);
    }

    private string GetAutoAdvanceLabel(int seconds) =>
        seconds == 0 ? L["AutoAdvanceOff"] : string.Format(L["AutoAdvanceSeconds"], seconds);

    // The size saved on the row stays offered even when it is not one of the usual choices.
    private IReadOnlyList<int> PageSizeOptions
    {
        get
        {
            var options = Kind == RowKind.Banner ? _bannerPageSizeOptions : _pageSizeOptions;
            return options.Contains(_pageSize) ? options : [.. options.Append(_pageSize).Order()];
        }
    }

    // A ready-made row can use a sort that is not offered for new rows: keep it selectable.
    private IReadOnlyList<MediaOrderingOption> OrderOptions =>
        _orderOptions.Contains(_orderBy) ? _orderOptions : [.. _orderOptions, _orderBy];

    private RowKind Kind => _displayType.IsBanner() ? RowKind.Banner : RowKind.Carousel;

    private bool BannerTaken => !string.IsNullOrEmpty(ExistingBannerTitle);

    private IReadOnlyList<ButtonGroupOption<RowKind>> KindOptions => BannerTaken
        ? [new(RowKind.Carousel, L["KindCarousel"], Phosphor.Rows)]
        :
        [
            new(RowKind.Carousel, L["KindCarousel"], Phosphor.Rows),
            new(RowKind.Banner, L["KindBanner"], Phosphor.Star)
        ];

    private string KindHint
    {
        get
        {
            if (BannerTaken)
                return string.Format(L["KindBannerTakenHint"], ExistingBannerTitle);

            return Kind == RowKind.Banner ? L["KindBannerHint"] : L["KindCarouselHint"];
        }
    }

    // The second choice depends on the first: a card format for a carousel, a style for a banner.
    private IReadOnlyList<ButtonGroupOption<HomeRowDisplayType>> DisplayOptions => Kind == RowKind.Banner
        ?
        [
            new(HomeRowDisplayType.Featured, L["BannerContained"]),
            new(HomeRowDisplayType.FeaturedBleed, L["BannerBleed"])
        ]
        :
        [
            new(HomeRowDisplayType.Carousel, L["DisplayCarousel"]),
            new(HomeRowDisplayType.Landscape, L["DisplayLandscape"])
        ];

    private string DisplayHint => _displayType switch
    {
        HomeRowDisplayType.Landscape => L["DisplayLandscapeHint"],
        HomeRowDisplayType.Featured => L["BannerContainedHint"],
        HomeRowDisplayType.FeaturedBleed => L["BannerBleedHint"],
        _ => L["DisplayCarouselHint"]
    };

    // Switching family lands on its first choice.
    private void OnKindChanged(RowKind kind)
    {
        if (kind == Kind)
            return;

        _displayType = kind == RowKind.Banner ? HomeRowDisplayType.Featured : HomeRowDisplayType.Carousel;
        _pageSize = kind == RowKind.Banner ? HomeRowDisplay.DefaultBannerCount : 20;
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
        MediaOrderingOption.RecommendedForYou => L["OrderRecommendedForYou"],
        MediaOrderingOption.BecauseYouWatched => L["OrderBecauseYouWatched"],
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
            Title = _titleKey is not null && _title.Trim() == _translatedTitle ? _titleKey : _title.Trim(),
            DisplayType = _displayType,
            ContinueWatching = _continueWatching,
            LibraryIds = libraryIds,
            LibraryGroupIds = libraryGroupIds,
            MediaTypes = _continueWatching ? [] : new List<MediaType>(_mediaTypes),
            OrderBy = _orderBy,
            PageSize = _pageSize,
            AutoAdvanceSeconds = Kind == RowKind.Banner && _autoAdvanceSeconds > 0 ? _autoAdvanceSeconds : null,
            IsVisible = InitialModel?.IsVisible ?? true,
            Order = InitialModel?.Order ?? 0
        };
        Dialog.Close(K7DialogResult.Ok(model));
    }
}
