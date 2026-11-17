using K7.Clients.Shared.Helpers;
using Microsoft.JSInterop;

namespace K7.Clients.Shared.UI.Components.Players;

/// <summary>
/// Embedded PGS on the Video.js player: the .sup sidecar is parsed here and each decoded
/// screen is handed to a canvas over the video (k7Pgs* in videoplayer.js), so the server
/// does not burn the track into a transcode.
/// </summary>
public partial class VideoPlayer
{
    private PgsSubtitleLoader? _pgsLoader;
    private (Guid FileId, int TrackIndex)? _pgsKey;
    private string? _pgsPlayerId;
    private int _pgsGeneration;

    private async Task SyncPgsOverlayAsync()
    {
        if (!_isInitialized || string.IsNullOrEmpty(_player.Id))
            return;

        var source = PlayerService.Source;
        var track = PlayerService.SelectedSubtitleTrack;
        (Guid FileId, int TrackIndex)? wanted =
            track is not null
            && source?.IndexedFileId is Guid fileId
            && ClientBitmapSubtitles.ShouldDraw(track, source.MimeType, source.Url)
                ? (fileId, track.Index)
                : null;

        if (wanted == _pgsKey && _pgsPlayerId == _player.Id)
            return;

        await StopPgsOverlayAsync();
        if (wanted is not { } key)
            return;

        var generation = _pgsGeneration;
        var playerId = _player.Id;
        var loader = new PgsSubtitleLoader(PgsSubtitleLoader.ForServer(
            K7ServerService.HttpClient,
            relative => K7ServerService.GetAbsoluteUri(relative),
            key.FileId,
            key.TrackIndex));
        loader.Reset += () => InvokePgs(generation, "k7PgsReset", playerId);
        loader.LoadedUntilChanged += seconds => InvokePgs(generation, "k7PgsSetLoadedUntil", playerId, seconds);
        loader.FrameLoaded += frame => PushPgsFrame(generation, playerId, frame);

        _pgsLoader = loader;
        _pgsKey = key;
        _pgsPlayerId = playerId;

        try
        {
            await JSRuntime.InvokeVoidAsync("k7PgsStart", playerId);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
        {
            return;
        }

        loader.Update(PlayerService.CurrentTime);
    }

    private async Task StopPgsOverlayAsync()
    {
        _pgsGeneration++;
        var playerId = _pgsPlayerId;
        _pgsLoader?.Dispose();
        _pgsLoader = null;
        _pgsKey = null;
        _pgsPlayerId = null;
        if (string.IsNullOrEmpty(playerId))
            return;

        try
        {
            await JSRuntime.InvokeVoidAsync("k7PgsStop", playerId);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
        {
        }
    }

    private void PushPgsFrame(int generation, string playerId, PgsFrame frame)
    {
        // One interop call per screen: a composition holds at most two objects, and a
        // single call cannot interleave with the next screen's.
        var count = Math.Min(frame.Objects.Count, 2);
        var layout = new int[count * 4];
        for (var i = 0; i < count; i++)
        {
            var pgsObject = frame.Objects[i];
            layout[i * 4] = pgsObject.X;
            layout[(i * 4) + 1] = pgsObject.Y;
            layout[(i * 4) + 2] = pgsObject.Width;
            layout[(i * 4) + 3] = pgsObject.Height;
        }

        InvokePgs(
            generation,
            "k7PgsAddFrame",
            playerId,
            frame.TimeSeconds,
            frame.PlaneWidth,
            frame.PlaneHeight,
            layout,
            count > 0 ? frame.Objects[0].Rgba : null,
            count > 1 ? frame.Objects[1].Rgba : null);
    }

    private void InvokePgs(int generation, string identifier, params object?[] args)
    {
        _ = InvokeAsync(async () =>
        {
            if (generation != _pgsGeneration)
                return;

            try
            {
                await JSRuntime.InvokeVoidAsync(identifier, args);
            }
            catch (Exception ex) when (ex is JSException or InvalidOperationException or JSDisconnectedException or ObjectDisposedException)
            {
            }
        });
    }
}
