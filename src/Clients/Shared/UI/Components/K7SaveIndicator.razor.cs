using Microsoft.AspNetCore.Components;

namespace K7.Clients.Shared.UI.Components;

/// <summary>
/// State of a settings page that saves on change: spinner while a change waits or is being
/// sent, round check once saved, warning when the save failed. Empty while idle, and always
/// rendered so screen readers hear the changes.
/// </summary>
public partial class K7SaveIndicator
{
    [Parameter] public SettingsSaveState State { get; set; }
    [Parameter] public string Class { get; set; } = "";
}
