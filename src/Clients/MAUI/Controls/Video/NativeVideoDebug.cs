namespace K7.Clients.MAUI.Controls.Video;

/// <summary>
/// Temporary native-video diagnostics for adb logcat. Filter with:
/// adb logcat -s K7NativeVideo:I *:S
/// Set <see cref="Enabled"/> to false (or delete call sites) once playback is validated.
/// </summary>
internal static class NativeVideoDebug
{
    public const string Tag = "K7NativeVideo";

    /// <summary>Flip to false to silence without removing call sites.</summary>
    public static bool Enabled { get; set; } = true;

    public static void Log(string message)
    {
        if (!Enabled)
            return;

#if ANDROID
        global::Android.Util.Log.Info(Tag, message);
#else
        System.Diagnostics.Debug.WriteLine("[" + Tag + "] " + message);
#endif
    }
}
