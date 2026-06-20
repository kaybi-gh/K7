namespace K7.Shared.Dtos;

public class ServerInfoDto
{
    public bool GuestEnabled { get; set; }
    public string DefaultLanguage { get; set; } = "en";
    public string DefaultTheme { get; set; } = "default-dark";
}
