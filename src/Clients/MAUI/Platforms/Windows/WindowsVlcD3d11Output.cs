#if WINDOWS
using System.Globalization;
using System.Runtime.InteropServices;
using K7.Clients.MAUI.Playback;
using LibVLCSharp;
using SharpDX.Direct3D11;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using DeviceContext = SharpDX.Direct3D11.DeviceContext;
using MediaPlayer = LibVLCSharp.MediaPlayer;

namespace K7.Clients.MAUI.Platforms.Windows;

/// <summary>
/// LibVLC 4 desktop dropped <c>--winrt-d3dcontext</c> / <c>--winrt-swapchain</c>.
/// LibVLCSharp's <c>OutputConfig</c> is smaller than native
/// <c>libvlc_video_output_cfg_t</c> (8-byte union vs 16), so managed writes
/// never reach <c>transfer</c>/<c>orientation</c> and VLC asserts in es_format.c.
/// Bind D3D11 through <c>libvlc_video_set_output_callbacks</c> with the C layout.
/// </summary>
internal sealed unsafe class WindowsVlcD3d11Output : IDisposable
{
    private const int LibVlcEngineD3D11 = 3;
    private const int LibVlcColorSpaceBt709 = 2;
    private const int LibVlcPrimariesBt709 = 3;
    private const int LibVlcTransferSrgb = 2;
    private const int LibVlcOrientTopLeft = 0;

    private readonly object _gate = new();
    private readonly NativeSetup _setup;
    private readonly NativeCleanup _cleanup;
    private readonly NativeSetWindow _setWindow;
    private readonly NativeUpdateOutput _updateOutput;
    private readonly NativeSwap _swap;
    private readonly NativeMakeCurrent _makeCurrent;

    private Device? _device;
    private DeviceContext? _context;
    private SwapChain1? _swapChain;
    private RenderTargetView? _rtv;
    private NativeResize? _reportSize;
    private IntPtr _reportOpaque;
    private IntPtr _contextMutex;
    private bool _disposed;
    private bool _firstPresented;

    public event Action? FirstPresented;

    public WindowsVlcD3d11Output()
    {
        _setup = OnNativeSetup;
        _cleanup = OnNativeCleanup;
        _setWindow = OnNativeSetWindow;
        _updateOutput = OnNativeUpdateOutput;
        _swap = OnNativeSwap;
        _makeCurrent = OnNativeMakeCurrent;
    }

    public bool TryAttach(MediaPlayer player, string[] swapChainOptions)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!TryParse(swapChainOptions, out var contextPtr, out var swapChainPtr))
            return false;

        lock (_gate)
        {
            ReleaseViews();
            ReleaseAdoptedDevice();
            _firstPresented = false;

            try
            {
                _context = new DeviceContext(contextPtr);
                _swapChain = new SwapChain1(swapChainPtr);
                _device = _swapChain.GetDevice<Device>();
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("vlc d3d11 adopt failed " + ex.GetType().Name);
                ReleaseAdoptedDevice();
                return false;
            }

            if (_device is null || _device.IsDisposed)
            {
                VlcPlayerLog.Warn("vlc d3d11 device missing from swapchain");
                ReleaseAdoptedDevice();
                return false;
            }

            EnableMultithread(_device);
            EnsureContextMutex();
            if (!TryCreateRtv())
                return false;
        }

        var attached = libvlc_video_set_output_callbacks(
            player.NativeReference,
            LibVlcEngineD3D11,
            _setup,
            _cleanup,
            _setWindow,
            _updateOutput,
            _swap,
            _makeCurrent,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);
        if (!attached)
            VlcPlayerLog.Warn("vlc d3d11 native callbacks rejected");
        else
            VlcPlayerLog.Info("vlc d3d11 native output callbacks");

        return attached;
    }

    public void NotifyPixelSize(uint width, uint height)
    {
        if (width == 0 || height == 0)
            return;

        var locked = TryLockContext();
        try
        {
            lock (_gate)
            {
                if (_swapChain is null || _swapChain.IsDisposed)
                    return;

                ReleaseRtv();
                _swapChain.ResizeBuffers(0, (int)width, (int)height, Format.Unknown, SwapChainFlags.None);
                TryCreateRtv();
            }
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc d3d11 resize failed " + ex.GetType().Name);
        }
        finally
        {
            if (locked)
                UnlockContext();
        }

        _reportSize?.Invoke(_reportOpaque, width, height);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        // Callers must Detach(player) first so VLC stops invoking Present.
        _disposed = true;
        lock (_gate)
        {
            ReleaseViews();
            ReleaseAdoptedDevice();
        }
    }

    /// <summary>
    /// App-exit path: mark dead and drop SharpDX wrappers without Dispose.
    /// WinUI owns the SwapChainPanel device; disposing it while Closing tears down the
    /// panel races Present and surfaces as ExecutionEngineException.
    /// </summary>
    public void SoftReleaseForAppExit()
    {
        _disposed = true;
        lock (_gate)
        {
            ReleaseViews();
            _swapChain = null;
            _context = null;
            _device = null;
            if (_contextMutex != IntPtr.Zero)
            {
                try
                {
                    CloseHandle(_contextMutex);
                }
                catch
                {
                }

                _contextMutex = IntPtr.Zero;
            }
        }
    }

    /// <summary>
    /// Unregister output callbacks before disposing the MediaPlayer / SwapChainPanel.
    /// Present after WinUI destroys the swapchain is a common AccessViolation on exit.
    /// </summary>
    public void Detach(MediaPlayer? player)
    {
        if (player is not null && player.NativeReference != IntPtr.Zero)
        {
            try
            {
                libvlc_video_set_output_callbacks(
                    player.NativeReference,
                    LibVlcEngineD3D11,
                    null!,
                    null!,
                    null!,
                    null!,
                    null!,
                    null!,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);
            }
            catch (Exception ex)
            {
                VlcPlayerLog.Warn("vlc d3d11 detach " + ex.GetType().Name);
            }
        }

        lock (_gate)
        {
            ReleaseViews();
        }
    }

    internal static bool TryParse(string[] options, out IntPtr deviceContext, out IntPtr swapChain)
    {
        deviceContext = IntPtr.Zero;
        swapChain = IntPtr.Zero;
        foreach (var option in options)
        {
            if (TryReadHexPointer(option, "--winrt-d3dcontext=0x", out var context))
                deviceContext = context;
            else if (TryReadHexPointer(option, "--winrt-swapchain=0x", out var chain))
                swapChain = chain;
        }

        return deviceContext != IntPtr.Zero && swapChain != IntPtr.Zero;
    }

    private static bool TryReadHexPointer(string option, string prefix, out IntPtr pointer)
    {
        pointer = IntPtr.Zero;
        if (!option.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var hex = option[prefix.Length..];
        if (!ulong.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
            return false;

        pointer = (IntPtr)value;
        return pointer != IntPtr.Zero;
    }

    [return: MarshalAs(UnmanagedType.I1)]
    private bool OnNativeSetup(ref IntPtr opaque, IntPtr cfg, NativeSetupDeviceInfo* setup)
    {
        if (_disposed)
            return false;

        lock (_gate)
        {
            if (_disposed || _context is null || _context.IsDisposed || setup is null)
                return false;

            setup->DeviceContext = _context.NativePointer;
            setup->ContextMutex = _contextMutex;
            return true;
        }
    }

    private void OnNativeCleanup(IntPtr opaque)
    {
    }

    private void OnNativeSetWindow(
        IntPtr opaque,
        NativeResize reportSizeChange,
        IntPtr mouseMove,
        IntPtr mousePress,
        IntPtr mouseRelease,
        IntPtr reportOpaque)
    {
        if (_disposed)
            return;

        _reportSize = reportSizeChange;
        _reportOpaque = reportOpaque;
        if (!TryGetBufferSize(out var width, out var height))
            return;

        reportSizeChange?.Invoke(reportOpaque, width, height);
    }

    [return: MarshalAs(UnmanagedType.I1)]
    private bool OnNativeUpdateOutput(IntPtr opaque, NativeRenderCfg* config, NativeVideoOutputCfg* output)
    {
        if (_disposed || output is null)
            return false;

        *output = default;
        output->DxgiFormat = (int)Format.B8G8R8A8_UNorm;
        output->FullRange = 1;
        output->ColorSpace = LibVlcColorSpaceBt709;
        output->Primaries = LibVlcPrimariesBt709;
        output->Transfer = LibVlcTransferSrgb;
        output->Orientation = LibVlcOrientTopLeft;
        return TryCreateRtv();
    }

    private void OnNativeSwap(IntPtr opaque)
    {
        if (_disposed)
            return;

        lock (_gate)
        {
            if (_disposed || _swapChain is null || _swapChain.IsDisposed)
                return;

            try
            {
                _swapChain.Present(0, PresentFlags.None);
            }
            catch
            {
                return;
            }

            if (_firstPresented)
                return;

            _firstPresented = true;
        }

        FirstPresented?.Invoke();
    }

    [return: MarshalAs(UnmanagedType.I1)]
    private bool OnNativeMakeCurrent(IntPtr opaque, [MarshalAs(UnmanagedType.I1)] bool enter)
    {
        if (!enter)
            return true;

        if (_disposed)
            return false;

        lock (_gate)
        {
            if (_disposed || _context is null || _context.IsDisposed || _rtv is null || _rtv.IsDisposed)
                return false;

            try
            {
                _context.OutputMerger.SetTargets(_rtv);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    private bool TryGetBufferSize(out uint width, out uint height)
    {
        width = 0;
        height = 0;
        lock (_gate)
        {
            if (_swapChain is null || _swapChain.IsDisposed)
                return false;

            var desc = _swapChain.Description1;
            if (desc.Width <= 0 || desc.Height <= 0)
                return false;

            width = (uint)desc.Width;
            height = (uint)desc.Height;
            return true;
        }
    }

    private bool TryCreateRtv()
    {
        lock (_gate)
        {
            if (_device is null || _context is null || _swapChain is null
                || _device.IsDisposed || _context.IsDisposed || _swapChain.IsDisposed)
                return false;

            ReleaseRtv();
            using var backBuffer = _swapChain.GetBackBuffer<Texture2D>(0);
            _rtv = new RenderTargetView(_device, backBuffer);
            return true;
        }
    }

    private void ReleaseRtv()
    {
        try
        {
            _rtv?.Dispose();
        }
        catch
        {
        }

        _rtv = null;
    }

    private void ReleaseViews()
    {
        ReleaseRtv();
        _reportSize = null;
        _reportOpaque = IntPtr.Zero;
    }

    /// <summary>
    /// Drop SharpDX wrappers for VideoView-owned D3D objects. Prefer nulling over
    /// Dispose when WinUI may already have torn down the SwapChainPanel (app exit).
    /// </summary>
    private void ReleaseAdoptedDevice()
    {
        ReleaseRtv();
        TryDisposeCom(_swapChain);
        _swapChain = null;
        TryDisposeCom(_context);
        _context = null;
        TryDisposeCom(_device);
        _device = null;
        if (_contextMutex != IntPtr.Zero)
        {
            CloseHandle(_contextMutex);
            _contextMutex = IntPtr.Zero;
        }
    }

    private static void TryDisposeCom(IDisposable? resource)
    {
        if (resource is null)
            return;

        try
        {
            resource.Dispose();
        }
        catch
        {
        }
    }

    private void EnsureContextMutex()
    {
        if (_contextMutex != IntPtr.Zero)
            return;

        _contextMutex = CreateMutex(IntPtr.Zero, false, null);
    }

    private bool TryLockContext()
    {
        if (_contextMutex == IntPtr.Zero)
            return false;

        return WaitForSingleObject(_contextMutex, 1000) == 0;
    }

    private void UnlockContext()
    {
        if (_contextMutex != IntPtr.Zero)
            ReleaseMutex(_contextMutex);
    }

    private static void EnableMultithread(Device device)
    {
        try
        {
            using var multithread = device.QueryInterface<Multithread>();
            multithread.SetMultithreadProtected(true);
        }
        catch (Exception ex)
        {
            VlcPlayerLog.Warn("vlc d3d11 multithread " + ex.GetType().Name);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeSetupDeviceInfo
    {
        public IntPtr DeviceContext;
        public IntPtr ContextMutex;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRenderCfg
    {
        public uint Width;
        public uint Height;
        public uint BitDepth;
        public byte FullRange;
        public int ColorSpace;
        public int Primaries;
        public int Transfer;
        public IntPtr Device;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct NativeVideoOutputCfg
    {
        [FieldOffset(0)] public int DxgiFormat;
        [FieldOffset(16)] public byte FullRange;
        [FieldOffset(20)] public int ColorSpace;
        [FieldOffset(24)] public int Primaries;
        [FieldOffset(28)] public int Transfer;
        [FieldOffset(32)] public int Orientation;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool NativeSetup(ref IntPtr opaque, IntPtr cfg, NativeSetupDeviceInfo* setup);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeCleanup(IntPtr opaque);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeSetWindow(
        IntPtr opaque,
        NativeResize reportSizeChange,
        IntPtr mouseMove,
        IntPtr mousePress,
        IntPtr mouseRelease,
        IntPtr reportOpaque);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeResize(IntPtr reportOpaque, uint width, uint height);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool NativeUpdateOutput(IntPtr opaque, NativeRenderCfg* config, NativeVideoOutputCfg* output);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void NativeSwap(IntPtr opaque);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    private delegate bool NativeMakeCurrent(IntPtr opaque, [MarshalAs(UnmanagedType.I1)] bool enter);

    [DllImport("libvlc", CallingConvention = CallingConvention.Cdecl, EntryPoint = "libvlc_video_set_output_callbacks")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool libvlc_video_set_output_callbacks(
        IntPtr mediaPlayer,
        int engine,
        NativeSetup setup,
        NativeCleanup cleanup,
        NativeSetWindow setWindow,
        NativeUpdateOutput updateOutput,
        NativeSwap swap,
        NativeMakeCurrent makeCurrent,
        IntPtr getProcAddress,
        IntPtr metadata,
        IntPtr selectPlane,
        IntPtr opaque);

    [DllImport("kernel32", SetLastError = true)]
    private static extern IntPtr CreateMutex(IntPtr attributes, bool initialOwner, string? name);

    [DllImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CloseHandle(IntPtr handle);

    [DllImport("kernel32", SetLastError = true)]
    private static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);

    [DllImport("kernel32", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ReleaseMutex(IntPtr handle);
}
#endif
