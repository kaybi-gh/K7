namespace K7.Server.Domain.Settings;

public sealed class SettingKey<T>(string name)
{
    public string Name { get; } = name;
}
