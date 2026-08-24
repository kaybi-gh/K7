using K7.Server.Domain.Enums;
using K7.Shared.Dtos;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.Helpers;

/// <summary>
/// Pushes subtitle appearance settings into the Video.js cue stylesheet (Web / Windows).
/// </summary>
public static class SubtitleStyleApplicator
{
    public static async Task ApplyAsync(
        IJSRuntime js,
        VideoPlayerSettingsDto settings,
        DeviceType deviceType = DeviceType.Desktop,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(js);
        ArgumentNullException.ThrowIfNull(settings);

        var css = SubtitleStyleHelper.ToCss(settings, deviceType);
        try
        {
            await PlaybackAssetLoader.EnsureAsync(js, cancellationToken);

            await js.InvokeVoidAsync(
                "applySubtitleStyle",
                cancellationToken,
                new
                {
                    fontFamily = css.FontFamily,
                    fontSize = css.FontSize,
                    color = css.Color,
                    backgroundColor = css.BackgroundColor,
                    textShadow = css.TextShadow
                });
        }
        catch (JSException)
        {
        }
        catch (JSDisconnectedException)
        {
        }
        catch (ObjectDisposedException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }
}
