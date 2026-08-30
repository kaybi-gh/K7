using System.Runtime.InteropServices;

namespace K7.Clients.MAUI.Platforms.Windows.Services;

/// <summary>
/// WASAPI volume for the LibVLC video session only (mixer name "K7").
/// Does not touch the MAUI / WebView2 session ("K7.Clients.MAUI").
/// LibVLC <c>MediaPlayer.Volume</c> is software gain and does not move that slider.
/// </summary>
internal static class WindowsAppAudioVolume
{
    private const int Render = 0;
    private const int Multimedia = 1;
    private const int ClsCtxAll = 23;
    private static readonly Guid PlayerGroupingId = new("c8e2a91f-4b7d-4e6a-9f13-2d8c1a5b0e77");

    public static bool TryGet(out double volume01)
    {
        var current = 1.0;
        var found = false;
        TryForEachPlayerSession((simple, control) =>
        {
            simple.GetMasterVolume(out var level);
            current = Math.Clamp(level, 0, 1);
            found = true;
            TryRenamePlayerSession(control);
            return false;
        });
        volume01 = current;
        return found;
    }

    public static bool TrySet(double volume01)
    {
        var level = (float)Math.Clamp(volume01, 0, 1);
        var context = Guid.Empty;
        var updated = 0;
        TryForEachPlayerSession((simple, control) =>
        {
            if (simple.SetMasterVolume(level, ref context) == 0)
                updated++;
            TryRenamePlayerSession(control);
            return true;
        });

        return updated > 0;
    }

    private static bool TryForEachPlayerSession(Func<ISimpleAudioVolume, IAudioSessionControl, bool> onSession)
    {
        IMMDeviceEnumerator? enumerator = null;
        IMMDevice? device = null;
        IAudioSessionManager2? manager = null;
        IAudioSessionEnumerator? sessions = null;
        try
        {
            enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
            if (enumerator.GetDefaultAudioEndpoint(Render, Multimedia, out device) != 0 || device is null)
                return false;

            var iid = typeof(IAudioSessionManager2).GUID;
            if (device.Activate(ref iid, ClsCtxAll, nint.Zero, out var raw) != 0 || raw is null)
                return false;

            manager = (IAudioSessionManager2)raw;
            if (manager.GetSessionEnumerator(out sessions) != 0 || sessions is null)
                return false;

            sessions.GetCount(out var count);
            var pid = (uint)Environment.ProcessId;
            var any = false;
            for (var i = 0; i < count; i++)
            {
                object? rawSession = null;
                try
                {
                    if (sessions.GetSession(i, out rawSession) != 0 || rawSession is null)
                        continue;

                    if (rawSession is not IAudioSessionControl control)
                        continue;

                    var name = ReadDisplayName(control);
                    var sessionPid = 0u;
                    string? sessionId = null;
                    if (rawSession is IAudioSessionControl2 control2)
                    {
                        control2.GetProcessId(out sessionPid);
                        if (control2.GetSessionIdentifier(out var identifier) == 0)
                            sessionId = identifier;
                    }

                    if (sessionPid != pid || !LooksLikePlayerSession(name, sessionId))
                        continue;

                    if (rawSession is not ISimpleAudioVolume simple)
                        continue;

                    any = true;
                    if (!onSession(simple, control))
                        return true;
                }
                finally
                {
                    Release(rawSession);
                }
            }

            return any;
        }
        catch (COMException)
        {
            return false;
        }
        finally
        {
            Release(sessions);
            Release(manager);
            Release(device);
            Release(enumerator);
        }
    }

    private static string ReadDisplayName(IAudioSessionControl control)
    {
        return control.GetDisplayName(out var name) == 0 && name is not null
            ? name
            : "";
    }

    private static bool LooksLikePlayerSession(string? name, string? sessionId)
    {
        if (!string.IsNullOrEmpty(sessionId)
            && (sessionId.Contains("vlc", StringComparison.OrdinalIgnoreCase)
                || sessionId.Contains("libvlc", StringComparison.OrdinalIgnoreCase)))
            return true;

        if (string.IsNullOrEmpty(name))
            return false;

        if (name.Contains("K7.Clients", StringComparison.OrdinalIgnoreCase))
            return false;

        if (name.Equals("K7", StringComparison.OrdinalIgnoreCase))
            return true;

        return name.Contains("VLC", StringComparison.OrdinalIgnoreCase)
            || name.Contains("LibVLC", StringComparison.OrdinalIgnoreCase);
    }

    private static void TryRenamePlayerSession(IAudioSessionControl control)
    {
        var name = ReadDisplayName(control);
        var context = Guid.Empty;
        if (!name.Equals("K7", StringComparison.OrdinalIgnoreCase))
            control.SetDisplayName("K7", ref context);

        var grouping = PlayerGroupingId;
        control.SetGroupingParam(ref grouping, ref context);
    }

    private static void Release(object? com)
    {
        if (com is not null)
            Marshal.ReleaseComObject(com);
    }

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    private class MMDeviceEnumerator;

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out nint devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object? interfacePointer);
    }

    [ComImport]
    [Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionManager2
    {
        [PreserveSig]
        int GetAudioSessionControl(ref Guid audioSessionGuid, uint streamFlags, out nint sessionControl);

        [PreserveSig]
        int GetSimpleAudioVolume(ref Guid audioSessionGuid, uint streamFlags, out ISimpleAudioVolume? volume);

        [PreserveSig]
        int GetSessionEnumerator(out IAudioSessionEnumerator? sessionEnum);
    }

    [ComImport]
    [Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionEnumerator
    {
        [PreserveSig]
        int GetCount(out int count);

        [PreserveSig]
        int GetSession(int sessionCount, [MarshalAs(UnmanagedType.IUnknown)] out object? session);
    }

    [ComImport]
    [Guid("F4B1A599-7266-4319-A8BA-D2B174CB7E5C")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl
    {
        [PreserveSig]
        int GetState(out int state);

        [PreserveSig]
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        [PreserveSig]
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

        [PreserveSig]
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);

        [PreserveSig]
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

        [PreserveSig]
        int GetGroupingParam(out Guid grouping);

        [PreserveSig]
        int SetGroupingParam(ref Guid grouping, ref Guid eventContext);

        [PreserveSig]
        int RegisterAudioSessionNotification(nint newNotifications);

        [PreserveSig]
        int UnregisterAudioSessionNotification(nint newNotifications);
    }

    [ComImport]
    [Guid("bfb13ffb-3515-475d-9446-68d987541182")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioSessionControl2
    {
        [PreserveSig]
        int GetState(out int state);

        [PreserveSig]
        int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        [PreserveSig]
        int SetDisplayName([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

        [PreserveSig]
        int GetIconPath([MarshalAs(UnmanagedType.LPWStr)] out string path);

        [PreserveSig]
        int SetIconPath([MarshalAs(UnmanagedType.LPWStr)] string value, ref Guid eventContext);

        [PreserveSig]
        int GetGroupingParam(out Guid grouping);

        [PreserveSig]
        int SetGroupingParam(ref Guid grouping, ref Guid eventContext);

        [PreserveSig]
        int RegisterAudioSessionNotification(nint newNotifications);

        [PreserveSig]
        int UnregisterAudioSessionNotification(nint newNotifications);

        [PreserveSig]
        int GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);

        [PreserveSig]
        int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)] out string retVal);

        [PreserveSig]
        int GetProcessId(out uint processId);
    }

    [ComImport]
    [Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISimpleAudioVolume
    {
        [PreserveSig]
        int SetMasterVolume(float level, ref Guid eventContext);

        [PreserveSig]
        int GetMasterVolume(out float level);

        [PreserveSig]
        int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid eventContext);

        [PreserveSig]
        int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
