using System.Collections.Frozen;

namespace K7.Server.Domain.Settings;

public static class ServerSettingKeys
{
    public static readonly SettingKey<bool> SetupCompleted = new("SetupCompleted");
    public static readonly SettingKey<bool> LocalRegistrationEnabled = new("LocalRegistrationEnabled", defaultValue: true);
    public static readonly SettingKey<bool> OidcAutoProvisioningEnabled = new("OidcAutoProvisioningEnabled", defaultValue: true);

    public static readonly FrozenDictionary<string, ISettingKey> AdminEditableKeys = new Dictionary<string, ISettingKey>
    {
        [LocalRegistrationEnabled.Name] = LocalRegistrationEnabled,
        [OidcAutoProvisioningEnabled.Name] = OidcAutoProvisioningEnabled,
    }.ToFrozenDictionary();
}
