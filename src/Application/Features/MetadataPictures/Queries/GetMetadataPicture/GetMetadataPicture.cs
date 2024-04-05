using MediaServer.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MediaServer.Application.Features.MetadataPictures.Queries.GetMetadataPicture;

//[Authorize]
public record GetMetadataPictureQuery(int Id) : IRequest<IResult>;

public class GetMetadataPictureQueryHandler : IRequestHandler<GetMetadataPictureQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetMetadataPictureQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetMetadataPictureQuery query, CancellationToken cancellationToken)
    {
        var entity = await _context.MetadataPictures
            .FindAsync([query.Id], cancellationToken);

        Guard.Against.NotFound(query.Id, entity);

        var bytes = File.ReadAllBytes(entity.Path);
        return Results.File(bytes, contentType: "image/jpeg");
        // TODO - Return real content type
    }
}
