using MediaServer.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MediaServer.Application.Features.MediaPictures.Queries.GetMediaPicture;

//[Authorize]
public record GetMediaPictureQuery(int Id) : IRequest<IResult>;

public class GetMediaPictureQueryHandler : IRequestHandler<GetMediaPictureQuery, IResult>
{
    private readonly IApplicationDbContext _context;

    public GetMediaPictureQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IResult> Handle(GetMediaPictureQuery query, CancellationToken cancellationToken)
    {
        var entity = await _context.MediaPictures
            .FindAsync([query.Id], cancellationToken);

        Guard.Against.NotFound(query.Id, entity);

        var bytes = File.ReadAllBytes(entity.Path);
        return Results.File(bytes, contentType: "image/jpeg");
    }
}
