using K7.Server.Application.Common.Models;

namespace K7.Server.Web.Infrastructure;

public static class HttpContentResultMapper
{
    public static IResult ToIResult(this HttpContentResult result) => result switch
    {
        FileHttpContentResult file => Results.File(
            file.FilePath,
            contentType: file.ContentType,
            enableRangeProcessing: file.EnableRangeProcessing,
            fileDownloadName: file.FileDownloadName),
        StreamHttpContentResult stream => Results.File(
            stream.OpenStream(),
            contentType: stream.ContentType,
            fileDownloadName: stream.FileDownloadName),
        BytesHttpContentResult bytes => Results.Bytes(bytes.Content, contentType: bytes.ContentType),
        TextHttpContentResult text => Results.Text(text.Content, text.ContentType, statusCode: text.StatusCode),
        EmptyHttpContentResult empty => Results.StatusCode(empty.StatusCode),
        JsonHttpContentResult json => Results.Json(json.Body, statusCode: json.StatusCode),
        CreatedHttpContentResult created => Results.Created(created.Location, created.Body),
        ConflictHttpContentResult conflict => Results.Conflict(conflict.Body),
        RedirectHttpContentResult redirect => redirect.Permanent
            ? Results.Redirect(redirect.Location, permanent: true)
            : Results.Redirect(redirect.Location),
        _ => throw new ArgumentOutOfRangeException(nameof(result), result, "Unsupported HTTP content result type.")
    };
}
