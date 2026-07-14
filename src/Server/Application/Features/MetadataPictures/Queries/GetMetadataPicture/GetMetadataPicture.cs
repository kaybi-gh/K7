using K7.Server.Application.Common.Helpers;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models;
using K7.Server.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace K7.Server.Application.Features.MetadataPictures.Queries.GetMetadataPicture;

//[Authorize]
public record GetMetadataPictureQuery(Guid Id, MetadataPictureSize? Size = null) : IRequest<HttpContentResult>;

public class GetMetadataPictureQueryHandler : IRequestHandler<GetMetadataPictureQuery, HttpContentResult>
{
    private readonly IApplicationDbContext _context;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public GetMetadataPictureQueryHandler(IApplicationDbContext context, IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<HttpContentResult> Handle(GetMetadataPictureQuery query, CancellationToken cancellationToken)
    {
        var entity = await _context.MetadataPictures
            .Include(p => p.Variants)
            .FirstOrDefaultAsync(p => p.Id == query.Id, cancellationToken);

        if (entity is null)
        {
            return new EmptyHttpContentResult(404);
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
            return new EmptyHttpContentResult(404);
        }

        var file = new FileInfo(localPath);
        if (!file.Exists)
        {
            return new EmptyHttpContentResult(404);
        }

        var contentType = GetContentType(file.Extension);

        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null)
        {
            return new StreamHttpContentResult(file.OpenRead, contentType);
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
                return new EmptyHttpContentResult(StatusCodes.Status304NotModified);
            }
        }
        else if (requestHeaders.IfModifiedSince is { } ifModifiedSince
                 && lastModified <= ifModifiedSince)
        {
            return new EmptyHttpContentResult(StatusCodes.Status304NotModified);
        }

        return new StreamHttpContentResult(file.OpenRead, contentType);
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
