using System.Net;

namespace K7.Clients.Shared.UI.Helpers;

/// <summary>
/// Classifies playback start failures coming back from the server.
/// </summary>
internal static class PlaybackErrorHelper
{
    /// <summary>
    /// True when the server rejected the stream session with 422: the file is indexed but not
    /// probed yet (MediaNotReadyException server side). Callers should show the
    /// "MediaPreparingPlayback" message instead of a raw error.
    /// </summary>
    public static bool IsMediaNotReady(Exception exception) =>
        exception is HttpRequestException { StatusCode: HttpStatusCode.UnprocessableEntity };
}
