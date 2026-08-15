using K7.Server.Domain.Enums;

namespace K7.Shared.Dtos;

public sealed record GeneralPreferencesDto
{
    /// <summary>
    /// Per library-group tap overrides on Explore. Missing keys fall back to
    /// <see cref="Entities.LibraryGroupDto.ExploreTapAction"/>.
    /// </summary>
    public Dictionary<Guid, ExploreTapAction> ExploreTapActions { get; set; } = [];

    public ExploreTapAction ResolveExploreTapAction(Guid libraryGroupId, ExploreTapAction fallback) =>
        ExploreTapActions.TryGetValue(libraryGroupId, out var action) ? action : fallback;
}
