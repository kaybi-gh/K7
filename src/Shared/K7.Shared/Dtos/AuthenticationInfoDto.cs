namespace K7.Shared.Dtos;

public class AuthenticationInfoDto
{
    public bool LocalSignInEnabled { get; set; }
    public bool LocalRegistrationEnabled { get; set; }
    public bool OidcEnabled { get; set; }
    public string? OidcDisplayName { get; set; }
    public bool OidcAutomaticAccountCreation { get; set; }
}
