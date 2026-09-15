using K7.Server.Domain.Enums;
using K7.Shared.CustomNav;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Enums;

namespace K7.Clients.Shared.Models;

public sealed class CustomNavItemEditModel
{
    public Guid Id { get; set; }
    public CustomNavItemKind Kind { get; set; } = CustomNavItemKind.LibraryGroup;
    public string? Title { get; set; }
    public Guid? LibraryGroupId { get; set; }
    public ExploreTapAction? TapAction { get; set; }
    public string? BrowseQuery { get; set; }
    public string? Route { get; set; }
    public Guid? TargetId { get; set; }
    public string? Icon { get; set; }
    public string? CardColor { get; set; }
    public Guid? CoverPictureId { get; set; }

    public static CustomNavItemEditModel FromDto(CustomNavItemDto dto) => new()
    {
        Id = dto.Id,
        Kind = dto.Kind,
        Title = dto.Title,
        LibraryGroupId = dto.LibraryGroupId,
        TapAction = dto.TapAction,
        BrowseQuery = dto.BrowseQuery,
        Route = dto.Route,
        TargetId = dto.TargetId,
        Icon = dto.Icon,
        CardColor = dto.CardColor,
        CoverPictureId = dto.CoverPictureId
    };

    public CustomNavItemDto ToDto() => new()
    {
        Id = Id == Guid.Empty ? Guid.NewGuid() : Id,
        Kind = Kind,
        Title = Title,
        LibraryGroupId = LibraryGroupId,
        TapAction = TapAction,
        BrowseQuery = CustomNavRoutes.NormalizeBrowseQuery(BrowseQuery),
        Route = Route,
        TargetId = TargetId,
        Icon = Icon,
        CardColor = CardColor,
        CoverPictureId = CoverPictureId
    };
}
