using K7.Shared.Dtos.CustomNav;

namespace K7.Clients.Shared.Interfaces;

/// <summary>
/// SignalR hub events for custom navigation layout (implemented by <see cref="Services.K7HubClient"/>).
/// </summary>
public interface ICustomNavHubEvents
{
    event Action<CustomNavLayoutDto>? CustomNavLayoutUpdated;
}
