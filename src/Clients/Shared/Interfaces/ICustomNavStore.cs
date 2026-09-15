using K7.Shared.Dtos;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;

namespace K7.Clients.Shared.Interfaces;

public interface ICustomNavStore
{
    event Action? Changed;

    bool IsLoaded { get; }

    CustomNavLayoutDto Layout { get; }

    IReadOnlyList<LibraryGroupDto> Groups { get; }

    IReadOnlyList<LiteCollectionDto> Collections { get; }

    IReadOnlyList<LitePlaylistDto> Playlists { get; }

    GeneralPreferencesDto GeneralPreferences { get; }

    Task EnsureLoadedAsync(CancellationToken cancellationToken = default);

    Task ReloadAsync(CancellationToken cancellationToken = default);

    void Invalidate();
}
