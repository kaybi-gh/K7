using K7.Clients.Shared.Interfaces;

namespace K7.Clients.Shared.Services;

public sealed class ExternalPlayerPolicy : IExternalPlayerPolicy
{
    public bool SuppressExternalPlayer { get; set; }
}
