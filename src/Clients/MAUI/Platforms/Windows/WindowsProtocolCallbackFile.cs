using K7.Clients.MAUI.Services.Authentication;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Fallback IPC when WinUI redirects the second process without a Protocol
/// activation payload. Only <c>k7://</c> values are accepted.
/// </summary>
internal static class WindowsProtocolCallbackFile
{
    internal static string FilePath { get; set; } = Path.Combine(Path.GetTempPath(), "k7-protocol-callback.uri");

    public static void Write(Uri uri)
    {
        if (!WindowsProtocolActivation.IsK7Callback(uri))
            return;

        File.WriteAllText(FilePath, uri.AbsoluteUri);
        NativeAuthTrace.Write("protocol-file", uri.GetLeftPart(UriPartial.Path));
    }

    public static Uri? TryReadAndDelete()
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            try
            {
                if (!File.Exists(FilePath))
                    return null;

                string raw;
                using (var stream = new FileStream(FilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var reader = new StreamReader(stream))
                    raw = reader.ReadToEnd().Trim();

                File.Delete(FilePath);

                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri) || !WindowsProtocolActivation.IsK7Callback(uri))
                    return null;

                return uri;
            }
            catch (IOException)
            {
                Thread.Sleep(20);
            }
            catch
            {
                return null;
            }
        }

        return null;
    }
}
