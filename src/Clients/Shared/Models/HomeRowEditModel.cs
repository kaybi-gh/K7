using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Home;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;

namespace K7.Clients.Shared.Models;

public sealed class HomeRowEditModel
{
    public Guid Id { get; init; }
    public string Title { get; set; } = string.Empty;
    public HomeRowDisplayType DisplayType { get; set; } = HomeRowDisplayType.Carousel;
    public bool ContinueWatching { get; set; }
    public List<Guid> LibraryIds { get; set; } = [];
    public List<MediaType> MediaTypes { get; set; } = [];
    public MediaOrderingOption OrderBy { get; set; } = MediaOrderingOption.CreatedDesc;
    public int PageSize { get; set; } = 20;
    public bool IsVisible { get; set; } = true;
    public int Order { get; set; }

    public static HomeRowEditModel FromDto(HomeRowConfigDto dto) => new()
    {
        Id = dto.Id,
        Title = dto.Title,
        DisplayType = dto.DisplayType,
        ContinueWatching = dto.ContinueWatching,
        LibraryIds = dto.LibraryIds?.ToList() ?? [],
        MediaTypes = dto.MediaTypes?.ToList() ?? [],
        OrderBy = dto.OrderBy?.FirstOrDefault() ?? MediaOrderingOption.CreatedDesc,
        PageSize = dto.PageSize,
        IsVisible = dto.IsVisible,
        Order = dto.Order
    };

    public HomeRowConfigDto ToDto() => new()
    {
        Id = Id,
        Title = Title,
        DisplayType = DisplayType,
        ContinueWatching = ContinueWatching,
        LibraryIds = ContinueWatching ? null : (LibraryIds.Count > 0 ? LibraryIds.AsReadOnly() : null),
        MediaTypes = ContinueWatching ? null : (MediaTypes.Count > 0 ? MediaTypes.AsReadOnly() : null),
        OrderBy = ContinueWatching ? null : [OrderBy],
        PageSize = PageSize,
        IsVisible = IsVisible,
        Order = Order
    };
}
