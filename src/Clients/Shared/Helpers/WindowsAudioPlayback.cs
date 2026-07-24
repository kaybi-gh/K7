namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Windows MAUI uses WebView2 HTML5 / Web Audio for music (EQ, crossfade, gapless)
/// instead of CommunityToolkit MediaElement.
/// </summary>
public static class WindowsAudioPlayback
{
    public static bool UsesWebAudioPlayer => OperatingSystem.IsWindows();
}
