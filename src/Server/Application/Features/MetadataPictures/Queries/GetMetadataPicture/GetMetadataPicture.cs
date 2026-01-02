using K7.Server.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace K7.Server.Application.Features.MetadataPictures.Queries.GetMetadataPicture;

//[Authorize]
public record GetMetadataPictureQuery(Guid Id) : IRequest<IResult>;

public class GetMetadataPictureQueryHandler : IRequestHandler<GetMetadataPictureQuery, IResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetMetadataPictureQueryHandler(IApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IResult> Handle(GetMetadataPictureQuery query, CancellationToken cancellationToken)
    {
        var entity = await _context.MetadataPictures
            .FindAsync([query.Id], cancellationToken);

        Guard.Against.NotFound(query.Id, entity);

        // Picture not yet downloaded
        if (entity.LocalPath == null)
        {
            return Results.NotFound();
        }

        var file = new FileInfo(entity.LocalPath);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        var httpContext = _httpContextAccessor.HttpContext;

        // If we don't have an HTTP context (e.g. background usage), just return the file
        if (httpContext is null)
        {
            return Results.File(file.OpenRead(), contentType: file.Extension switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                _ => "application/octet-stream"
            });
        }

        var eTag = new EntityTagHeaderValue($"\"{entity.Id}-{entity.LastModified.ToUnixTimeSeconds()}\"");

        httpContext.Response.GetTypedHeaders().LastModified = entity.LastModified;
        httpContext.Response.GetTypedHeaders().ETag = eTag;

        var requestHeaders = httpContext.Request.GetTypedHeaders();

        // Handle If-None-Match first (ETag validation)
        if (requestHeaders.IfNoneMatch is { Count: > 0 })
        {
            if (requestHeaders.IfNoneMatch.Any(tag => tag.Tag.Equals(eTag.Tag, StringComparison.Ordinal)))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }
        }
        // Fallback to If-Modified-Since if no ETag header is present
        else if (requestHeaders.IfModifiedSince is { } ifModifiedSince
                 && entity.LastModified <= ifModifiedSince)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.File(file.OpenRead(), contentType: file.Extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        });
        // TODO - Manage mime-type in database or with a better class
    }
}
