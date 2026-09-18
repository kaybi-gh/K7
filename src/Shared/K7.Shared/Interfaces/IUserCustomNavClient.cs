using K7.Shared.Dtos.CustomNav;

namespace K7.Shared.Interfaces;

/// <summary>
/// Client-side interface for custom navigation layout SignalR updates.
/// </summary>
public interface IUserCustomNavClient
{
    /// <summary>
    /// Receives the effective custom navigation layout for the connected user.
    /// </summary>
    Task ReceiveCustomNavLayoutUpdated(CustomNavLayoutDto layout);
}
