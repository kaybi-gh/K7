using MediaClient.Shared.Domain.Enums;

namespace MediaClient.Shared.Services;

public interface ICurrentDeviceService
{
    /// <summary>
    /// Returns device type.
    /// <para>Can only be called after FirstRender (<see cref="ComponentBase.OnAfterRenderAsync"/>).</para>
    /// </summary>
    /// <returns><see cref="DeviceType"/></returns>
    public ValueTask<DeviceType> GetDeviceTypeAsync();

    /// <summary>
    /// Returns device orientation.
    /// <para>Can only be called after FirstRender (<see cref="ComponentBase.OnAfterRenderAsync"/>).</para>
    /// </summary>
    /// <returns><see cref="DeviceOrientation"/></returns>
    public ValueTask<DeviceOrientation> GetDeviceOrientationAsync();

    /// <summary>
    /// Returns device OS.
    /// <para>Can only be called after FirstRender (<see cref="ComponentBase.OnAfterRenderAsync"/>).</para>
    /// </summary>
    /// <returns><see cref="DeviceOS"/></returns>
    public ValueTask<DeviceOS> GetDeviceOSAsync();
}
