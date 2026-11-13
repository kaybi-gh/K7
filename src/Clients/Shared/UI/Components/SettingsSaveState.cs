namespace K7.Clients.Shared.UI.Components;

/// <summary>State of an auto-saved settings page, shown by <see cref="K7SettingsSplit"/>.</summary>
public enum SettingsSaveState
{
    Idle,
    Saving,
    Saved,
    Failed
}
