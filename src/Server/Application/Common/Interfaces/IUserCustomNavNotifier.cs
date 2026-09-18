using K7.Shared.Dtos.CustomNav;

namespace K7.Server.Application.Common.Interfaces;

/// <summary>
/// Notifies connected clients about custom navigation layout changes for a specific user.
/// </summary>
public interface IUserCustomNavNotifier
{
    Task NotifyCustomNavLayoutUpdatedAsync(
        string identityUserId,
        CustomNavLayoutDto layout,
        CancellationToken cancellationToken = default);
}
