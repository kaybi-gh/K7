namespace K7.Server.Application.Common.Models;

public abstract record HttpContentResult;

public sealed record FileHttpContentResult(string FilePath, string ContentType, bool EnableRangeProcessing = true, string? FileDownloadName = null) : HttpContentResult;

public sealed record StreamHttpContentResult(Func<Stream> OpenStream, string ContentType, string? FileDownloadName = null) : HttpContentResult;

public sealed record BytesHttpContentResult(byte[] Content, string ContentType) : HttpContentResult;

public sealed record TextHttpContentResult(string Content, string ContentType, int StatusCode = 200) : HttpContentResult;

public sealed record EmptyHttpContentResult(int StatusCode) : HttpContentResult;

public sealed record JsonHttpContentResult(object Body, int StatusCode = 200) : HttpContentResult;

public sealed record CreatedHttpContentResult(string Location, object Body) : HttpContentResult;

public sealed record ConflictHttpContentResult(object Body) : HttpContentResult;

public sealed record RedirectHttpContentResult(string Location, bool Permanent = false) : HttpContentResult;
