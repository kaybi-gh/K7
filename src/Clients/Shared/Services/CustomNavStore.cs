using K7.Clients.Shared.Interfaces;
using K7.Shared.Dtos;
using K7.Shared.Dtos.CustomNav;
using K7.Shared.Dtos.Entities;
using K7.Shared.Dtos.Entities.Collections;
using K7.Shared.Dtos.Entities.Playlists;
using K7.Shared.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace K7.Clients.Shared.Services;

public sealed class CustomNavStore : ICustomNavStore, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICustomNavHubEvents _hubEvents;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public CustomNavStore(IServiceScopeFactory scopeFactory, ICustomNavHubEvents hubEvents)
    {
        _scopeFactory = scopeFactory;
        _hubEvents = hubEvents;
        _hubEvents.CustomNavLayoutUpdated += OnCustomNavLayoutUpdated;
    }

    public event Action? Changed;

    public bool IsLoaded { get; private set; }

    public CustomNavLayoutDto Layout { get; private set; } = CustomNavLayoutDto.Disabled();

    public IReadOnlyList<LibraryGroupDto> Groups { get; private set; } = [];

    public IReadOnlyList<LiteCollectionDto> Collections { get; private set; } = [];

    public IReadOnlyList<LitePlaylistDto> Playlists { get; private set; } = [];

    public GeneralPreferencesDto GeneralPreferences { get; private set; } = new();

    public Task EnsureLoadedAsync(CancellationToken cancellationToken = default) =>
        IsLoaded ? Task.CompletedTask : ReloadAsync(cancellationToken);

    public async Task ReloadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var preferences = scope.ServiceProvider.GetRequiredService<IUserPreferencesService>();
            var libraries = scope.ServiceProvider.GetRequiredService<ILibraryService>();
            var collections = scope.ServiceProvider.GetRequiredService<ICollectionService>();
            var playlists = scope.ServiceProvider.GetRequiredService<IPlaylistService>();

            try
            {
                Layout = await preferences.GetCustomNavLayoutAsync(cancellationToken);
            }
            catch
            {
                Layout = CustomNavLayoutDto.Disabled();
            }

            try
            {
                Groups = await libraries.GetLibraryGroupsAsync(cancellationToken);
            }
            catch
            {
                Groups = [];
            }

            try
            {
                var page = await collections.GetCollectionsAsync(1, 100, cancellationToken: cancellationToken);
                Collections = page?.Items?.ToList() ?? [];
            }
            catch
            {
                Collections = [];
            }

            try
            {
                var page = await playlists.GetPlaylistsAsync(1, 100, cancellationToken: cancellationToken);
                Playlists = page?.Items?.ToList() ?? [];
            }
            catch
            {
                Playlists = [];
            }

            try
            {
                GeneralPreferences = await preferences.GetEffectiveGeneralPreferencesAsync(cancellationToken);
            }
            catch
            {
                GeneralPreferences = new GeneralPreferencesDto();
            }

            IsLoaded = true;
        }
        finally
        {
            _gate.Release();
        }

        Changed?.Invoke();
    }

    public void Invalidate()
    {
        IsLoaded = false;
        Layout = CustomNavLayoutDto.Disabled();
        Groups = [];
        Collections = [];
        Playlists = [];
        GeneralPreferences = new GeneralPreferencesDto();
        Changed?.Invoke();
    }

    public void Dispose()
    {
        _hubEvents.CustomNavLayoutUpdated -= OnCustomNavLayoutUpdated;
        _gate.Dispose();
    }

    private void OnCustomNavLayoutUpdated(CustomNavLayoutDto layout)
    {
        Layout = layout;
        IsLoaded = true;
        Changed?.Invoke();
    }
}
