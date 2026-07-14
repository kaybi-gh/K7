namespace K7.Server.Application.Common.Interfaces;

public interface IPeerUrlGuard
{
    void EnsureAllowedOutgoingUrl(string url);
}
