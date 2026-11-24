using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.SharedProfiles.Queries.SharedProfileExists;

public record SharedProfileExistsQuery(Guid Id) : IRequest<bool>;

public class SharedProfileExistsQueryHandler(IApplicationDbContext context)
    : IRequestHandler<SharedProfileExistsQuery, bool>
{
    public Task<bool> Handle(SharedProfileExistsQuery request, CancellationToken cancellationToken) =>
        context.SharedProfiles
            .AsNoTracking()
            .AnyAsync(group => group.Id == request.Id, cancellationToken);
}
