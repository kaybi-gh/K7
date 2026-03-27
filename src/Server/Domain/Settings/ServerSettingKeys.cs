namespace K7.Server.Domain.Settings;

public static class ServerSettingKeys
{
    public static readonly SettingKey<bool> SetupCompleted = new("SetupCompleted");
    public static readonly SettingKey<string> DefaultLanguage = new("DefaultLanguage", "en");
}
