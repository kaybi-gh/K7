using System.Text.Json.Serialization;
using System.Text.Json;
using K7.Server.Application.Common.Converters;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Models.Dtos;

namespace K7.Server.Application.Features.Medias.Queries.GetMedia;

public record GetMediaQuery(Guid Id) : IRequest<MediaDto>;

public class GetMediaQueryHandler : IRequestHandler<GetMediaQuery, MediaDto>
{
    private readonly IApplicationDbContext _context;
    private readonly IMapper _mapper;

    public GetMediaQueryHandler(IApplicationDbContext context, IMapper mapper)
    {
        _context = context;
        _mapper = mapper;
    }

    public async Task<MediaDto> Handle(GetMediaQuery request, CancellationToken cancellationToken)
    {
        var entity = await _context.Medias
            .AsNoTracking()
            .Include(x => x.Metadata)
                .ThenInclude(x => x!.ExternalIds)
            .Include(x => x.Metadata)
                .ThenInclude(x => x!.Pictures)
            .Include(x => x.Metadata)
                .ThenInclude(x => x!.Ratings)
            .Include(x => x.Metadata)
                .ThenInclude(x => x!.PersonRoles)
                    .ThenInclude(x => x.PortraitPicture)
            .Include(x => x.Metadata)
                .ThenInclude(x => x!.PersonRoles)
                    .ThenInclude(x => x.Person)
                        .ThenInclude(x => x.PortraitPicture)
            .Include(x => x.IndexedFiles)
            .Where(x => x.Id == request.Id)
            .SingleOrDefaultAsync(cancellationToken);

        Guard.Against.NotFound(request.Id, entity);
        return entity.ConvertToDto(_mapper);
    }
}
