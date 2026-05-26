namespace K7.Server.Application.Common.Interfaces;

public interface IAuthenticationSettings
{
    bool LocalSignInEnabled { get; }
    bool OidcEnabled { get; }
    string? OidcDisplayName { get; }
}
