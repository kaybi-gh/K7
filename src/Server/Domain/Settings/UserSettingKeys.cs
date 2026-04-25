namespace K7.Server.Domain.Settings;

public static class UserSettingKeys
{
    public static readonly SettingKey<string> Language = new("Language");
    public static readonly SettingKey<string> HomeLayout = new("HomeLayout");
}
