using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;

namespace K7.Server.Application.Common.Behaviours;

public interface IMediaScopedRequest
{
    Guid MediaId { get; }
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
            await accessGuard.EnsureAccessAsync(mediaRequest.MediaId, cancellationToken);

        return await next();
    }
}
