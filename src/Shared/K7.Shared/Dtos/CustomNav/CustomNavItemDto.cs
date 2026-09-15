using K7.Server.Domain.Enums;
using K7.Shared.Enums;

namespace K7.Shared.Dtos.CustomNav;

public sealed record CustomNavItemDto
{
    public required Guid Id { get; init; }
    public required CustomNavItemKind Kind { get; init; }
    public string? Title { get; init; }
    public Guid? LibraryGroupId { get; init; }
    public ExploreTapAction? TapAction { get; init; }
    public string? BrowseQuery { get; init; }
    public string? Route { get; init; }
    public Guid? TargetId { get; init; }
    public string? Icon { get; init; }
    public string? CardColor { get; init; }
    public Guid? CoverPictureId { get; init; }
}
