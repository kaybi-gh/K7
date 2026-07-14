using K7.Server.Application.Common.Helpers;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace K7.Server.Application.Features.MetadataPictures.Queries.GetMetadataPicture;

//[Authorize]
public record GetMetadataPictureQuery(Guid Id, MetadataPictureSize? Size = null) : IRequest<IResult>;

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
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == query.Id, cancellationToken);

        if (entity is null)
        {
            return Results.NotFound();
        }

        // Resolve the file path: use variant if requested, fallback to original
        string? localPath = entity.LocalPath;
        DateTimeOffset lastModified = entity.LastModified;
        Guid entityId = entity.Id;
        var isFallback = false;
        MetadataPictureSize? requestedSize = query.Size;

        if (requestedSize is { } size)
        {
            var variant = entity.Variants.FirstOrDefault(v => v.Size == size);
            if (variant is not null)
            {
                localPath = variant.LocalPath;
                lastModified = variant.LastModified;
                entityId = variant.Id;
            }
            else
            {
                isFallback = true;
            }
        }

        if (localPath is null)
        {
            return Results.NotFound();
        }

        var file = new FileInfo(localPath);
        if (!file.Exists)
        {
            return Results.NotFound();
        }

        var contentType = GetContentType(file.Extension);

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return Results.File(file.OpenRead(), contentType: contentType);
        }

        var eTag = new EntityTagHeaderValue($"\"{entityId}-{lastModified.ToUnixTimeSeconds()}\"");

        var responseHeaders = httpContext.Response.GetTypedHeaders();
        responseHeaders.LastModified = lastModified;
        responseHeaders.ETag = eTag;

        var allowLongTermCache = !isFallback
            || (requestedSize is { } fallbackSize
                && MetadataPictureVariantRules.IsPermanentVariantFallback(
                    entity.Type,
                    fallbackSize,
                    entity.OriginalWidth));

        if (!allowLongTermCache)
        {
            responseHeaders.CacheControl = new CacheControlHeaderValue
            {
                NoCache = true,
                NoStore = true
            };
        }
        else
        {
            responseHeaders.CacheControl = new CacheControlHeaderValue
            {
                Public = true,
                MaxAge = TimeSpan.FromDays(30),
                Extensions = { new NameValueHeaderValue("immutable") }
            };
        }

        var requestHeaders = httpContext.Request.GetTypedHeaders();

        if (requestHeaders.IfNoneMatch is { Count: > 0 })
        {
            if (requestHeaders.IfNoneMatch.Any(tag => tag.Tag.Equals(eTag.Tag, StringComparison.Ordinal)))
            {
                return Results.StatusCode(StatusCodes.Status304NotModified);
            }
        }
        else if (requestHeaders.IfModifiedSince is { } ifModifiedSince
                 && lastModified <= ifModifiedSince)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.File(file.OpenRead(), contentType: contentType);
    }

    private static string GetContentType(string extension) => extension.ToLowerInvariant() switch
    {
        ".webp" => "image/webp",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".png" => "image/png",
        ".svg" => "image/svg+xml",
        ".gif" => "image/gif",
        _ => "application/octet-stream"
    };
}
