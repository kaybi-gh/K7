namespace K7.Server.Application.Common.Exceptions;

/// <summary>
/// Thrown when playback is requested for a file that is indexed but not probed yet.
/// </summary>
/// <remarks>
/// A media becomes visible as soon as it is identified, while the container probe that yields codecs
/// and tracks is a separate background task. Until it has run, playback cannot be negotiated. This is a
/// transient, expected state rather than a server fault, so it maps to 422 with its own problem type
/// and the client can tell it apart from a genuine error and retry.
/// </remarks>
public class MediaNotReadyException : UnprocessableEntityException
{
    /// <summary>
    /// Problem type advertised in the 422 response so a client can tell this transient state apart from
    /// a genuinely unprocessable request.
    /// </summary>
    public const string ProblemType = "https://k7.media/problems/media-not-ready";

    public MediaNotReadyException(Guid indexedFileId)
        : base($"IndexedFile {indexedFileId} has not been probed yet, playback is not available.")
    {
        IndexedFileId = indexedFileId;
    }

    public Guid IndexedFileId { get; }
}
