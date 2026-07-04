using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.MediaFormats;

namespace K7.Server.Application.Features.MediaFormats.Queries.GetMediaFormats;

public record GetMediaFormatsQuery : IRequest<IEnumerable<BaseMediaFormat>>;

public class GetMediaFormatsQueryHandler : IRequestHandler<GetMediaFormatsQuery, IEnumerable<BaseMediaFormat>>
{
    public Task<IEnumerable<BaseMediaFormat>> Handle(GetMediaFormatsQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult(Constants.MediaFormats);
    }
}

