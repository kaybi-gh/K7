#if LINUX
using K7.Clients.MAUI.Playback;
using K7.Clients.MAUI.Platforms.Linux;
using K7.Clients.Shared.Helpers;

namespace K7.Clients.MAUI;

/// <summary>
/// GTK4 side of desktop LibVLC Direct Play: player factory, window keyboard controller,
/// fullscreen and the WebKitGTK JS bridge. Shared glue: <c>BlazorPage.DesktopVlc.cs</c>.
/// </summary>
public partial class BlazorPage
{
    private Gtk.EventControllerKey? _linuxKeyController;
    private Gtk.EventControllerMotion? _linuxMotionController;
    private Gtk.Window? _linuxKeyWindow;
    private double _linuxLastMotionX = double.NaN;
    private double _linuxLastMotionY = double.NaN;
    private LinuxRootLayoutDriver? _rootLayoutDriver;

    partial void InitializePlayerPlatform()
    {
        // The labs root layout never re-runs on maximize / fullscreen / visibility changes.
        _rootLayoutDriver ??= LinuxRootLayoutDriver.Attach(RootGrid);
        // Theme the overlay the moment GTK realizes it: at the first session start its widgets
        // were already styled by the theme and the first chrome came out as grey GTK buttons.
        RootGrid.DescendantAdded += OnLinuxRootDescendantAdded;
        if (_nativeOverlay is not null)
            ThemeLinuxOverlay(_nativeOverlay);
        blazorWebView.HandlerChanged += OnLinuxBlazorWebViewHandlerChanged;
        ConfigureWebKitSecurity();
        InitializeDesktopVlcRequests();
        _playerService.EnterFullScreenRequested += OnLinuxEnterFullScreen;
        _playerService.ExitFullScreenRequested += OnLinuxExitFullScreen;
        EnsureLinuxKeyController();
    }

    partial void DetachPlayerPlatform()
    {
        RootGrid.DescendantAdded -= OnLinuxRootDescendantAdded;
        _playerService.EnterFullScreenRequested -= OnLinuxEnterFullScreen;
        _playerService.ExitFullScreenRequested -= OnLinuxExitFullScreen;
        _directTrackOverrideUrl = null;
        try
        {
            _vlcPlayer?.PrepareForAppExit();
        }
        catch
        {
            StopDesktopVlc();
        }

        _vlcPlayer = null;
        _vlcEventsHooked = false;
        blazorWebView.HandlerChanged -= OnLinuxBlazorWebViewHandlerChanged;
        DetachLinuxKeyController();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        // The window handler may not exist yet during InitializePlayerPlatform.
        EnsureLinuxKeyController();
    }

    private partial IDesktopVlcVideoPlayer CreateDesktopVlcPlayer() => new LinuxVlcVideoPlayer(RootGrid);

    private void OnLinuxRootDescendantAdded(object? sender, ElementEventArgs e)
    {
        if (e.Element is Controls.Video.NativeVideoPlayerOverlay overlay)
            ThemeLinuxOverlay(overlay);
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Controls.Video.NativeVideoPlayerOverlay, object> _themedOverlays = new();

    private static void ThemeLinuxOverlay(Controls.Video.NativeVideoPlayerOverlay overlay)
    {
        void Theme()
        {
            LinuxOverlayTheme.Apply(LinuxWidgetStack.PlatformWidget(overlay));
            LinuxGestureBridge.Attach(overlay);
        }

        Theme();
        if (!_themedOverlays.TryGetValue(overlay, out _))
        {
            _themedOverlays.Add(overlay, overlay);
            overlay.HandlerChanged += (_, _) => Theme();
        }
    }

    /// <summary>
    /// Direct Play started: pin the GTK stack (decode surface at the bottom, WebView hidden for
    /// real, native overlay on top) and wire the overlay gestures. The labs BlazorWebView handler
    /// is not a <c>GtkViewHandler</c>: IsVisible / Opacity / InputTransparent never reach the
    /// WebKit widget, which otherwise stays painted and clickable above the overlay.
    /// </summary>
    partial void OnDesktopVlcSessionStarted()
    {
        SetLinuxWebViewShown(false);
        ApplyLinuxVideoStack("session-start");
        if (_nativeOverlay is not null)
        {
            LinuxOverlayTheme.Apply(LinuxWidgetStack.PlatformWidget(_nativeOverlay));
            LinuxGestureBridge.Attach(_nativeOverlay);
        }

        // Second dump once frames flow, to catch anything re-parented by the layout pass.
        GLib.Functions.TimeoutAdd(0, 3000, () =>
        {
            if (IsDesktopVlcActive)
                ApplyLinuxVideoStack("playing");
            return false;
        });
    }

    partial void OnDesktopVlcSessionStopped() => SetLinuxWebViewShown(true);

    private void SetLinuxWebViewShown(bool shown)
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not Gtk.Widget widget)
                return;

            widget.SetVisible(shown);
            widget.SetCanTarget(shown);
            VlcPlayerLog.Info("gtk webview shown=" + shown);
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("gtk webview visibility " + ex.GetType().Name + " " + ex.Message);
        }
    }

    /// <summary>
    /// HLS under native chrome: the WebView keeps painting Video.js but must not take pointer
    /// events (the overlay owns input, as InputTransparent would do on WinUI).
    /// </summary>
    private void SetLinuxWebViewInteractive(bool interactive)
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is Gtk.Widget widget)
            {
                widget.SetCanTarget(interactive);
                if (interactive)
                    NudgeLinuxWebViewRepaint(widget);
            }
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("gtk webview input " + ex.GetType().Name + " " + ex.Message);
        }
    }

    /// <summary>
    /// WebKitGTK sometimes keeps the last composited frame (black video) after the page
    /// content changed underneath native chrome: ask for a fresh layout and draw on idle.
    /// </summary>
    private static void NudgeLinuxWebViewRepaint(Gtk.Widget widget)
    {
        GLib.Functions.IdleAdd(0, () =>
        {
            try
            {
                // Unmap / map: WebKit drops the stale composited frame and draws the page.
                widget.SetVisible(false);
                widget.SetVisible(true);
                widget.QueueResize();
                widget.QueueDraw();
                if (FindWebKitView(widget) is { } webView)
                    webView.QueueDraw();
            }
            catch
            {
            }

            return false;
        });
    }

    private void ApplyLinuxVideoStack(string reason)
    {
        try
        {
            // MAUI ZIndex is the single source of truth for the GTK child order (video surface
            // 1, hidden WebView 2, close button 4, overlay 5, sidecar label and stats HUD 6).
            // The explicit send-to-back / bring-to-front pins that used to live here fought the
            // ZIndex sort of LinuxRootLayoutDriver on every layout tick ("gtk zsort ...
            // reordered" never converged) and left the subtitle label under the overlay panel.
            LinuxWidgetStack.SortByZIndex(RootGrid);
            LinuxWidgetStack.Dump(reason, LinuxWidgetStack.PlatformWidget(RootGrid));
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("gtk stack order " + ex.GetType().Name + " " + ex.Message);
        }
    }

    private static bool _libVlcUnavailableNotified;

    /// <summary>
    /// No usable libvlc (VLC 3 on the distro, or none): warn once, then let the player service
    /// promote the Direct Play session to the Video.js transcode ladder, like a Direct Play
    /// start failure on Windows. The overlay veil stays until Video.js paints.
    /// </summary>
    partial void OnDesktopVlcUnavailable(string detail, ref bool handled)
    {
        handled = true;
        StopDesktopVlc();

        if (!_libVlcUnavailableNotified)
        {
            _libVlcUnavailableNotified = true;
            var services = IPlatformApplication.Current?.Services;
            var snackbar = services?.GetService<K7.Clients.Shared.Interfaces.IK7Snackbar>();
            var localizer = services?.GetService<Microsoft.Extensions.Localization.IStringLocalizer<K7.Clients.Shared.UI.SharedResource>>();
            if (snackbar is not null && localizer is not null)
                snackbar.Add(localizer["LinuxLibVlcUnavailable"], K7.Clients.Shared.Models.K7Severity.Warning);
        }

        _ = FallBackToWebTranscodeAsync(detail);
    }

    private async Task FallBackToWebTranscodeAsync(string detail)
    {
        try
        {
            if (await _playerService.TryRecoverPlaybackStartAsync(allowQualityLadder: true))
            {
                VlcPlayerLog.Info("vlc unavailable -> video.js transcode");
                return;
            }

            VlcPlayerLog.Warn("vlc unavailable and no transcode fallback: " + detail);
            await _playerService.AbortPlaybackStartAsync("StreamNotReady");
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc fallback failed " + ex.GetType().Name + " " + ex.Message);
        }
    }

    partial void ConfigureDesktopVideoPlayerLayout()
    {
        NativePlayerCloseButton.IsVisible = false;

        if (_playerService.IsVisible)
        {
            BackgroundColor = Colors.Black;
            Padding = new Thickness(0);
            EnsureLinuxKeyController();
        }
        else
        {
            BackgroundColor = Color.FromRgb(13, 9, 7);
            blazorWebView.BackgroundColor = Color.FromRgb(13, 9, 7);
            TryEvaluateWebViewJs(
                "try{if(window.blankK7VideoSurfaces)blankK7VideoSurfaces();"
                + "if(window.K7&&K7.setNativePlayerActive)K7.setNativePlayerActive(false,false);}catch(e){}");
            StopDesktopVlc();
        }
    }

    private static Gtk.Window? TryGetLinuxWindow() =>
        Application.Current?.Windows.FirstOrDefault()?.Handler?.PlatformView as Gtk.Window;

    private void EnsureLinuxKeyController()
    {
        var window = TryGetLinuxWindow();
        if (window is null || ReferenceEquals(_linuxKeyWindow, window))
            return;

        DetachLinuxKeyController();
        var controller = Gtk.EventControllerKey.New();
        // Capture phase: the native overlay must see keys before WebKit grabs them.
        controller.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        controller.OnKeyPressed += OnLinuxKeyPressed;
        controller.OnKeyReleased += OnLinuxKeyReleased;
        window.AddController(controller);
        _linuxKeyController = controller;

        // Window-level motion: the native overlay hides its chrome after a timeout and
        // needs cursor activity to bring it back, whatever the widget under the pointer.
        var motion = Gtk.EventControllerMotion.New();
        motion.SetPropagationPhase(Gtk.PropagationPhase.Capture);
        motion.OnMotion += OnLinuxPointerMotion;
        window.AddController(motion);
        _linuxMotionController = motion;
        _linuxKeyWindow = window;
        VlcPlayerLog.Info("gtk input controllers attached");
    }

    private void DetachLinuxKeyController()
    {
        if (_linuxKeyController is null && _linuxMotionController is null)
            return;

        try
        {
            if (_linuxKeyController is not null)
            {
                _linuxKeyController.OnKeyPressed -= OnLinuxKeyPressed;
                _linuxKeyController.OnKeyReleased -= OnLinuxKeyReleased;
                _linuxKeyWindow?.RemoveController(_linuxKeyController);
            }

            if (_linuxMotionController is not null)
            {
                _linuxMotionController.OnMotion -= OnLinuxPointerMotion;
                _linuxKeyWindow?.RemoveController(_linuxMotionController);
            }
        }
        catch
        {
        }

        _linuxKeyController = null;
        _linuxMotionController = null;
        _linuxKeyWindow = null;
    }

    private bool LinuxNativeChromeActive =>
        _playerService.IsVisible && _nativeOverlay is { IsVisible: true } && MauiNativeVideoChrome.IsEnabled;

    private void OnLinuxPointerMotion(Gtk.EventControllerMotion sender, Gtk.EventControllerMotion.MotionSignalArgs args)
    {
        if (!LinuxNativeChromeActive)
            return;

        // GTK synthesizes a motion event (same coordinates) after every widget-tree change,
        // including the layout pass that hides the chrome: that is not user activity.
        if (args.X == _linuxLastMotionX && args.Y == _linuxLastMotionY)
            return;

        _linuxLastMotionX = args.X;
        _linuxLastMotionY = args.Y;
        _nativeOverlay?.NotifyDesktopPointerMoved();
    }

    /// <summary>
    /// Logs what the page knows about the HTML5 video (Video.js players, element source and
    /// readiness, media error, container visibility, HLS resource requests seen by the page)
    /// as "web video probe" lines: the WebKit console is not always reachable (Flatpak, remote
    /// desktop), this is.
    /// </summary>
    private const string LinuxWebVideoProbeJs = "(function(){try{var v=document.querySelector('video');var vc=document.querySelector('.video-container');var cs=vc?getComputedStyle(vc):null;var players=(window.videojs&&videojs.getPlayers)?Object.keys(videojs.getPlayers()):[];var err=(v&&v.error)?{code:v.error.code,msg:v.error.message}:null;var res=(window.performance&&performance.getEntriesByType)?performance.getEntriesByType('resource'):[];var hls=0;var last='';for(var i=0;i<res.length;i++){var n=res[i].name||'';if(/m3u8|\\.ts(\\?|$)|m4s|segment/i.test(n)){hls++;last=n;}}return JSON.stringify({href:location.href,visibility:document.visibilityState,k7:!!window.K7,setActive:typeof(window.K7&&K7.setNativePlayerActive),vjs:!!window.videojs,players:players,video:!!v,src:v?(v.currentSrc||v.getAttribute('src')||''):'',ready:v?v.readyState:-1,net:v?v.networkState:-1,paused:v?v.paused:null,time:v?v.currentTime:-1,err:err,container:cs?(cs.display+'/'+cs.visibility+'/'+cs.opacity):'-',mse:!!window.MediaSource,nativeHls:v?v.canPlayType('application/vnd.apple.mpegurl'):'-',tech:(function(){try{var p=(window.videojs&&players.length)?videojs.getPlayers()[players[0]]:null;return p?((p.tech_?p.tech_.name_:'-')+'/'+((p.tech_&&p.tech_.vhs)?'vhs':'native')):'-';}catch(e){return 'err';}})(),videos:document.querySelectorAll('video').length,origin:location.origin,ua:(navigator.userAgent||'').slice(-70),safari:!!(window.videojs&&videojs.browser&&videojs.browser.IS_ANY_SAFARI),gtkQuirks:!!(window.videojs&&videojs.browser&&videojs.browser.__k7LinuxGtk),mms:!!window.ManagedMediaSource,bridge:{ref:!!(window.K7&&K7._windowsStreamFetchRef),installed:!!window.__k7WindowsStreamXhrInstalled,xhrWrapped:!!(window.videojs&&videojs.xhr&&videojs.xhr.__k7WindowsStreamBridge)},player:(function(){try{var p=(window.videojs&&players.length)?videojs.getPlayers()[players[0]]:null;if(!p)return null;var el=p.tech_&&p.tech_.el_;var h=p.tech_&&p.tech_.vhs;var pc=h&&h.playlistController_;var pl=pc&&pc.mainPlaylistLoader_;return {techName:p.techName_,src:(p.currentSrc()||'').slice(0,80),type:p.currentType(),ready:p.readyState(),net:p.networkState(),paused:p.paused(),err:p.error()?{code:p.error().code,msg:p.error().message}:null,sameEl:el===v,elSrc:el?(el.getAttribute('src')||'').slice(0,60):'-',elCur:el?(el.currentSrc||'').slice(0,60):'-',ms:pc&&pc.mediaSource?pc.mediaSource.readyState:'-',msUrl:h&&h.mediaSourceUrl_?String(h.mediaSourceUrl_).slice(0,50):'-',loader:pl?pl.state:'-',hasMain:!!(h&&h.playlists&&h.playlists.main),pending:(pl&&pl.request)?1:0,usingMms:pc?!!pc.usingManagedMediaSource_:'-',sources:el?Array.prototype.map.call(el.querySelectorAll('source'),function(s){return (s.getAttribute('src')||'').slice(0,40);}):'-'};}catch(e){return 'err '+e;}})(),log:(window.__k7Log||[]).slice(-20),hlsRequests:hls,lastHls:last.slice(-80)});}catch(x){return 'probe-error '+x;}})()";

    internal void ScheduleLinuxWebVideoProbes()
    {
        foreach (var delayMs in new uint[] { 2000, 6000, 15000 })
        {
            var tag = "hls+" + (delayMs / 1000).ToString(System.Globalization.CultureInfo.InvariantCulture) + "s";
            GLib.Functions.TimeoutAdd(0, delayMs, () =>
            {
                ProbeLinuxWebVideo(tag);
                return false;
            });
        }
    }

    private void ProbeLinuxWebVideo(string tag)
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not Gtk.Widget root || FindWebKitView(root) is not { } webView)
            {
                VlcPlayerLog.Info("web video probe " + tag + " no webview");
                return;
            }

            webView.EvaluateJavascriptAsync(LinuxWebVideoProbeJs).ContinueWith(
                task =>
                {
                    string text;
                    if (task.IsFaulted)
                    {
                        text = "fail " + (task.Exception?.GetBaseException().Message ?? "");
                    }
                    else
                    {
                        try
                        {
                            text = task.Result?.ToString() ?? "null";
                        }
                        catch (Exception ex)
                        {
                            text = "fail " + ex.GetType().Name + " " + ex.Message;
                        }
                    }

                    VlcPlayerLog.Info("web video probe " + tag + " " + text);
                },
                TaskScheduler.Default);
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("web video probe " + tag + " " + ex.GetType().Name + " " + ex.Message);
        }
    }

    private bool OnLinuxKeyPressed(Gtk.EventControllerKey sender, Gtk.EventControllerKey.KeyPressedSignalArgs args)
    {
        if (args.Keyval == Gdk.Constants.KEY_F9)
        {
            // Diagnostic: GTK stack of the page root and of the overlay, sidecar label state,
            // HTML5 video state.
            ProbeLinuxWebVideo("f9");
            ApplyLinuxVideoStack("f9");
            LinuxWidgetStack.Dump("f9 overlay", LinuxWidgetStack.PlatformWidget(_nativeOverlay));
            LinuxWidgetStack.DumpLabels("f9 labels", LinuxWidgetStack.PlatformWidget(RootGrid));
            LinuxWidgetStack.DumpLayout("f9", RootGrid);
            LinuxWidgetStack.DumpLayout("f9", _nativeOverlay);
            return true;
        }

        return DispatchLinuxOverlayKey(args.Keyval, isKeyUp: false);
    }

    private void OnLinuxKeyReleased(Gtk.EventControllerKey sender, Gtk.EventControllerKey.KeyReleasedSignalArgs args) =>
        DispatchLinuxOverlayKey(args.Keyval, isKeyUp: true);

    private bool DispatchLinuxOverlayKey(uint keyval, bool isKeyUp)
    {
        // Native chrome over LibVLC or over Video.js HLS: the overlay owns the keyboard.
        if (!LinuxNativeChromeActive)
            return false;

        var key = MapLinuxKey(keyval);
        if (key is null)
            return false;

        var handled = TryHandleNativeVideoKey(key, isKeyUp);
        if (!isKeyUp)
            VlcPlayerLog.Info("gtk key " + key + " overlay=" + (_nativeOverlay is not null) + " handled=" + handled);
        if (handled)
            return true;

        if (key == "escape" && !isKeyUp)
        {
            if (_playerService is Services.PlayerService playerService)
                playerService.OnBackPressed();
            else
                DispatchBackAsEscape();
            return true;
        }

        return false;
    }

    private static string? MapLinuxKey(uint keyval)
    {
        if (keyval == Gdk.Constants.KEY_Escape)
            return "escape";
        if (keyval == Gdk.Constants.KEY_space)
            return "space";
        if (keyval == Gdk.Constants.KEY_Return || keyval == Gdk.Constants.KEY_KP_Enter)
            return "enter";
        if (keyval == Gdk.Constants.KEY_Left || keyval == Gdk.Constants.KEY_KP_Left)
            return "arrowleft";
        if (keyval == Gdk.Constants.KEY_Right || keyval == Gdk.Constants.KEY_KP_Right)
            return "arrowright";
        if (keyval == Gdk.Constants.KEY_Up || keyval == Gdk.Constants.KEY_KP_Up)
            return "arrowup";
        if (keyval == Gdk.Constants.KEY_Down || keyval == Gdk.Constants.KEY_KP_Down)
            return "arrowdown";
        if (keyval == Gdk.Constants.KEY_f || keyval == Gdk.Constants.KEY_F || keyval == Gdk.Constants.KEY_F11)
            return "f";
        if (keyval == Gdk.Constants.KEY_m || keyval == Gdk.Constants.KEY_M)
            return "m";
        if (keyval == Gdk.Constants.KEY_AudioPlay || keyval == Gdk.Constants.KEY_AudioPause)
            return "mediaplaypause";
        if (keyval == Gdk.Constants.KEY_AudioStop)
            return "mediastop";
        return null;
    }

    private Task OnLinuxEnterFullScreen()
    {
        TryGetLinuxWindow()?.Fullscreen();
        _playerService.IsFullScreen = true;
        _rootLayoutDriver?.Invalidate();
        return Task.CompletedTask;
    }

    private Task OnLinuxExitFullScreen()
    {
        TryGetLinuxWindow()?.Unfullscreen();
        _playerService.IsFullScreen = false;
        _rootLayoutDriver?.Invalidate();
        return Task.CompletedTask;
    }

    private void OnLinuxBlazorWebViewHandlerChanged(object? sender, EventArgs e) => ConfigureWebKitSecurity();

    /// <summary>
    /// Blazor content is served from the custom <c>app://</c> scheme. WebKit treats an
    /// unregistered custom scheme as a non-CORS, insecure origin: dynamic <c>import()</c> of
    /// ES modules (ApexCharts) fails with "Importing a module script failed" and secure-context
    /// APIs are unavailable. Registering the scheme fixes both. Safe to repeat.
    /// Do not mark the scheme local: WebKit then treats it like file and the page stays blank.
    /// </summary>
    private void ConfigureWebKitSecurity()
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not Gtk.Widget root)
                return;

            var webView = FindWebKitView(root);
            var securityManager = webView?.WebContext?.GetSecurityManager();
            if (securityManager is null)
                return;

            securityManager.RegisterUriSchemeAsCorsEnabled("app");
            securityManager.RegisterUriSchemeAsSecure("app");
            InjectLinuxHostMarker(webView!);
            EnableWebKitInspectorIfRequested(webView!);
            EnableWebKitConsoleIfRequested(webView!);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("K7 MAUI - WebKit security registration failed: " + ex.Message);
        }
    }

    private static bool _webKitInspectorShown;
    private static bool _webKitConsoleEnabled;

    /// <summary>
    /// <c>K7_WEBKIT_CONSOLE=1</c> copies the page console (console.log, JS errors, failed
    /// fetches) to the process stdout: the only way to see Blazor / Video.js errors from a
    /// terminal when the inspector window is not practical (Flatpak, remote desktop).
    /// </summary>
    private static void EnableWebKitConsoleIfRequested(WebKit.WebView webView)
    {
        if (_webKitConsoleEnabled
            || Environment.GetEnvironmentVariable("K7_WEBKIT_CONSOLE") is not "1")
            return;

        try
        {
            var settings = webView.GetSettings();
            settings.EnableWriteConsoleMessagesToStdout = true;
            webView.SetSettings(settings);
            _webKitConsoleEnabled = true;
            Console.Error.WriteLine("K7 MAUI - WebKit console copied to stdout");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("K7 MAUI - WebKit console failed: " + ex.Message);
        }
    }
    private static bool _linuxHostMarkerInjected;

    /// <summary>
    /// <c>window.K7_LINUX_GTK</c> lets the shared scripts pick GTK-specific paths (music through
    /// the loopback proxy instead of a blob). Injected at document start for every navigation,
    /// and set on the current document in case the page is already loaded.
    /// </summary>
    private static void InjectLinuxHostMarker(WebKit.WebView webView)
    {
        // The labs scheme handler looks assets up with their query string and answers 404
        // (Blazor-ApexCharts imports "...js?ver=x"). Blazor's "import" interop builds the URL
        // through new URL(...).toString(): drop the query for app:// URLs there.
        // Also keeps the last console warnings / errors and uncaught failures in
        // window.__k7Log for the "web video probe" lines (the WebKit console is not always
        // reachable), and widens the resource timing buffer the probe counts requests from.
        const string marker = "window.K7_LINUX_GTK = true;"
            + "(function(){try{var t=URL.prototype.toString;URL.prototype.toString=function(){var s=t.call(this);"
            + "return (this.protocol==='app:'&&this.search)?s.split('?')[0]:s;};}catch(e){}})();"
            + "(function(){try{if(window.__k7Log)return;var L=window.__k7Log=[];"
            + "function push(k,args){try{var m=k+': '+Array.prototype.map.call(args,function(a){try{return typeof a==='string'?a:((a&&a.message)?a.message:JSON.stringify(a));}catch(e){return String(a);}}).join(' ');"
            + "L.push(m.slice(0,300));if(L.length>60)L.shift();}catch(e){}}"
            + "['warn','error'].forEach(function(k){var o=console[k];console[k]=function(){push(k,arguments);return o.apply(console,arguments);};});"
            + "window.addEventListener('error',function(e){push('onerror',[(e.message||'')+' '+(e.filename||'')+':'+(e.lineno||'')]);});"
            + "window.addEventListener('unhandledrejection',function(e){push('unhandledrejection',[(e.reason&&e.reason.message)||String(e.reason)]);});"
            + "if(window.performance&&performance.setResourceTimingBufferSize)performance.setResourceTimingBufferSize(2000);"
            + "}catch(e){}})();";
        if (!_linuxHostMarkerInjected)
        {
            _linuxHostMarkerInjected = true;
            webView.GetUserContentManager().AddScript(
                WebKit.UserScript.New(
                    marker,
                    WebKit.UserContentInjectedFrames.AllFrames,
                    WebKit.UserScriptInjectionTime.Start,
                    null,
                    null));
        }

        _ = webView.EvaluateJavascriptAsync(marker);
    }

    /// <summary>
    /// <c>K7_WEBKIT_INSPECTOR=1</c> opens the WebKit Web Inspector (JS console, DOM, network)
    /// next to the app: the GTK host has no F12 equivalent to debug Blazor / navigation.js.
    /// </summary>
    private static void EnableWebKitInspectorIfRequested(WebKit.WebView webView)
    {
        if (_webKitInspectorShown
            || Environment.GetEnvironmentVariable("K7_WEBKIT_INSPECTOR") is not "1")
            return;

        try
        {
            var settings = webView.GetSettings();
            settings.EnableDeveloperExtras = true;
            webView.SetSettings(settings);
            webView.GetInspector()?.Show();
            _webKitInspectorShown = true;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine("K7 MAUI - WebKit inspector failed: " + ex.Message);
        }
    }

    /// <summary>
    /// The labs BlazorWebView handler wraps a <c>WebKit.WebView</c> in a <c>Gtk.Box</c>.
    /// WebKit calls must run on the GTK main loop.
    /// </summary>
    internal bool TryEvaluateWebViewJs(string script)
    {
        try
        {
            if (blazorWebView.Handler?.PlatformView is not Gtk.Widget root)
                return false;

            var webView = FindWebKitView(root);
            if (webView is null)
                return false;

            GLib.Functions.IdleAdd(0, () =>
            {
                try
                {
                    _ = webView.EvaluateJavascriptAsync(script);
                }
                catch
                {
                }

                return false;
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static WebKit.WebView? FindWebKitView(Gtk.Widget widget)
    {
        if (widget is WebKit.WebView webView)
            return webView;

        for (var child = widget.GetFirstChild(); child is not null; child = child.GetNextSibling())
        {
            var found = FindWebKitView(child);
            if (found is not null)
                return found;
        }

        return null;
    }
}
#endif
