using K7.Clients.Shared.Models;
using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Pages.Admin.Dialogs;

public partial class AdminHomeRowDialog
{
    private static readonly MediaType[] _availableMediaTypes =
        [MediaType.Movie, MediaType.MusicAlbum, MediaType.Serie];

    [CascadingParameter] private IK7DialogInstance Dialog { get; set; } = null!;

    [Parameter] public HomeRowEditModel? InitialModel { get; set; }
    [Parameter] public List<LibraryDto> Libraries { get; set; } = [];

    private string _title = "";
    private HomeRowDisplayType _displayType = HomeRowDisplayType.Carousel;
    private bool _continueWatching;
    private List<Guid> _libraryIds = [];
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
        _mediaTypes = new List<MediaType>(InitialModel.MediaTypes);
        _orderBy = InitialModel.OrderBy;
        _pageSize = InitialModel.PageSize;
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

    private void Cancel() => Dialog.Cancel();

    private void Submit()
    {
        var model = new HomeRowEditModel
        {
            Id = InitialModel?.Id ?? Guid.NewGuid(),
            Title = _title.Trim(),
            DisplayType = _displayType,
            ContinueWatching = _continueWatching,
            LibraryIds = _continueWatching ? [] : new List<Guid>(_libraryIds),
            MediaTypes = _continueWatching ? [] : new List<MediaType>(_mediaTypes),
            OrderBy = _orderBy,
            PageSize = _pageSize,
            IsVisible = InitialModel?.IsVisible ?? true,
            Order = InitialModel?.Order ?? 0
        };
        Dialog.Close(K7DialogResult.Ok(model));
    }
}
