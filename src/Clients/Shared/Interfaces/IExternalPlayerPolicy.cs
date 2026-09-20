namespace K7.Clients.Shared.Interfaces;

/// <summary>
/// When true, Windows MPC (and any future external player) must not handle Play.
/// Used while this device is the remote-play receiver so transport stays on the built-in player.
/// </summary>
public interface IExternalPlayerPolicy
{
    bool SuppressExternalPlayer { get; set; }
}
