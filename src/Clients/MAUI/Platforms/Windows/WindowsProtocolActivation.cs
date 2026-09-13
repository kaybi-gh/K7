using System.Diagnostics;
using K7.Clients.MAUI.Services.Authentication;
using Microsoft.Windows.AppLifecycle;
using OpenIddict.Client.SystemIntegration;
using Windows.ApplicationModel.Activation;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// Unpackaged WinUI redirects <c>k7://</c> to the already-running process via
/// <see cref="AppInstance.Activated"/>. OpenIddict only reads the URI at
/// startup, so the waiting Sign in must be notified from this event.
/// </summary>
internal static class WindowsProtocolActivation
{
    private static int _attached;
    private static FileSystemWatcher? _callbackWatcher;

    public static void Attach()
    {
        if (Interlocked.Exchange(ref _attached, 1) != 0)
            return;

        try
        {
            AppInstance.GetCurrent().Activated += OnActivated;
            WatchCallbackFile();
            NativeAuthTrace.Write("protocol-listen");
            TryConsumeCallbackFile();
        }
        catch (Exception ex)
        {
            Debug.WriteLine("K7 MAUI - protocol activation attach failed: " + ex);
        }
    }

    internal static Uri? TryGetStartupCallback()
    {
        return TryGetCommandLineCallback() ?? TryGetActivatedCallback();
    }

    internal static Uri? TryGetCommandLineCallback()
    {
        foreach (var argument in Environment.GetCommandLineArgs())
        {
            if (Uri.TryCreate(argument, UriKind.Absolute, out var uri) && IsK7Callback(uri))
                return uri;
        }

        return null;
    }

    internal static Uri? TryGetActivatedCallback()
    {
        try
        {
            return TryGetProtocolUri(AppInstance.GetCurrent().GetActivatedEventArgs());
        }
        catch
        {
            return null;
        }
    }

    public static void HandleUri(Uri? uri)
    {
        if (uri is null || !IsK7Callback(uri))
            return;

        var services = IPlatformApplication.Current?.Services;
        if (services is null)
        {
            NativeAuthTrace.Write("protocol-error", "no-services");
            return;
        }

        NativeAuthTrace.Write("protocol", uri.GetLeftPart(UriPartial.Path));
        _ = HandleAsync(services, uri);
    }

    internal static bool IsK7Callback(Uri uri) =>
        string.Equals(uri.Scheme, WindowsNativeProtocol.Scheme, StringComparison.OrdinalIgnoreCase);

    private static void OnActivated(object? sender, AppActivationArguments args)
    {
        NativeAuthTrace.Write("activated", args.Kind.ToString());
        HandleUri(TryGetProtocolUri(args) ?? WindowsProtocolCallbackFile.TryReadAndDelete());
    }

    private static void WatchCallbackFile()
    {
        var path = WindowsProtocolCallbackFile.FilePath;
        var directory = Path.GetDirectoryName(path);
        var name = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(name))
            return;

        var watcher = new FileSystemWatcher(directory, name)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };
        watcher.Created += (_, _) => TryConsumeCallbackFile();
        watcher.Changed += (_, _) => TryConsumeCallbackFile();
        watcher.EnableRaisingEvents = true;
        _callbackWatcher = watcher;
    }

    private static void TryConsumeCallbackFile()
    {
        var uri = WindowsProtocolCallbackFile.TryReadAndDelete();
        if (uri is not null)
            HandleUri(uri);
    }

    private static Uri? TryGetProtocolUri(AppActivationArguments args)
    {
        if (args.Kind != ExtendedActivationKind.Protocol)
            return null;

        if (args.Data is ProtocolActivatedEventArgs protocol)
            return protocol.Uri;

        if (args.Data is IProtocolActivatedEventArgs winrt)
            return winrt.Uri;

        return null;
    }

    private static async Task HandleAsync(IServiceProvider services, Uri uri)
    {
        try
        {
            var service = services.GetRequiredService<OpenIddictClientSystemIntegrationService>();
            await service.HandleProtocolActivationAsync(new OpenIddictClientSystemIntegrationActivation(uri))
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            NativeAuthTrace.Write("protocol-error", ex.GetType().Name);
            Debug.WriteLine("K7 MAUI - protocol activation failed: " + ex);
        }
    }
}
