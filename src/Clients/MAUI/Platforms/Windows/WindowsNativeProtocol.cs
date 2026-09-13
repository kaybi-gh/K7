using System.Diagnostics;
using Microsoft.Win32;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Registers the unpackaged <c>k7://</c> protocol under HKCU so the system browser
/// can return the authorization code without hitting http://localhost.
/// Per-user, no administrator rights. Same approach as the OpenIddict Sorgan sample.
/// </summary>
internal static class WindowsNativeProtocol
{
    internal const string Scheme = "k7";

    public static void EnsureRegistered()
    {
        var exe = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exe))
            return;

        try
        {
            using var root = Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Classes\" + Scheme);
            if (root is null)
                return;

            root.SetValue(string.Empty, "URL:K7");
            root.SetValue("URL Protocol", string.Empty);

            using var command = root.CreateSubKey(@"shell\open\command");
            command?.SetValue(string.Empty, BuildOpenCommand(exe));
            Debug.WriteLine("K7 MAUI - registered k7:// protocol for " + exe);
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - protocol registration failed: " + ex);
        }
    }

    internal static string BuildOpenCommand(string exe) => "\"" + exe + "\" \"%1\"";
}
