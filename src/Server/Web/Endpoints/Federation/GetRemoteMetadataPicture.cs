using K7.Server.Application.Features.Federation.Queries.GetRemoteMetadataPicture;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace K7.Server.Web.Endpoints.Federation;

public class GetRemoteMetadataPicture : IEndpoint
{
    public void Map(IEndpointRouteBuilder endpointRouteBuilder)
    {
        var type = GetType();
        var groupName = type.Namespace!.Split('.').Last();

        endpointRouteBuilder.MapGet(
            "/api/remote-indexed-files/{remoteFileId:guid}/metadata-pictures/{pictureId:guid}",
            async (
                Guid remoteFileId,
                Guid pictureId,
                [FromQuery] MetadataPictureSize? size,
                [FromServices] ISender sender,
                HttpContext httpContext,
                CancellationToken cancellationToken) =>
            {
                var result = await sender.Send(
                    new GetRemoteMetadataPictureQuery(remoteFileId, pictureId, size),
                    cancellationToken);

                if (result.StatusCode is < 200 or >= 300)
                    return Results.StatusCode(result.StatusCode);

                httpContext.Response.StatusCode = result.StatusCode;

                if (result.ContentType is not null)
                    httpContext.Response.ContentType = result.ContentType;

                if (result.ContentLength is not null)
                    httpContext.Response.ContentLength = result.ContentLength;

                foreach (var header in result.ForwardHeaders)
                    httpContext.Response.Headers[header.Key] = header.Value;

                if (result.Body is not null)
                    await result.Body.CopyToAsync(httpContext.Response.Body, cancellationToken);

                return Results.Empty;
            })
        .AllowAnonymous()
        .WithName(type.Name)
        .WithTags(groupName);
    }
}
