using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Requests;
using K7.Shared.Enums;

namespace K7.Shared.Dtos.Home;

public sealed record HomeRowConfigDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public required HomeRowDisplayType DisplayType { get; init; }

    /// <summary>
    /// Banner rows only: seconds before the banner moves to its next title on its own. Null keeps
    /// it still, the default.
    /// </summary>
    public int? AutoAdvanceSeconds { get; init; }
    public IReadOnlyList<Guid>? LibraryIds { get; init; }
    public IReadOnlyList<Guid>? LibraryGroupIds { get; init; }
    public IReadOnlyList<MediaType>? MediaTypes { get; init; }
    public IReadOnlyList<MediaOrderingOption>? OrderBy { get; init; }
    public required int PageSize { get; init; } = 20;
    public required bool ContinueWatching { get; init; }
    public required bool IsVisible { get; init; }
    public required int Order { get; init; }
}
