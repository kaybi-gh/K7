using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;

namespace K7.Server.Application.Common.Behaviours;

public interface IMediaScopedRequest
{
    Guid MediaId { get; }
}

/// <summary>
/// Media-scoped command that must still run when the media is hidden for the current user.
/// Used by unhide, which would otherwise be rejected as not found.
/// </summary>
public interface IAllowsExcludedMediaAccess
{
}

public class MediaAccessBehaviour<TRequest, TResponse>(IMediaAccessGuard accessGuard)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (request is IMediaScopedRequest mediaRequest)
        {
            if (request is IAllowsExcludedMediaAccess)
                await accessGuard.EnsureAccessIgnoringMediaExclusionAsync(mediaRequest.MediaId, cancellationToken);
            else
                await accessGuard.EnsureAccessAsync(mediaRequest.MediaId, cancellationToken);
        }

        return await next();
    }
}
