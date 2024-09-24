using MediaServer.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;

namespace MediaServer.Application.Features.MetadataPictures.Queries.GetMetadataPicture;

//[Authorize]
public record GetMetadataPictureQuery(Guid Id) : IRequest<IResult>;

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

        return Results.File(file.OpenRead(), contentType: file.Extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            _ => "application/octet-stream"
        });
        // TODO - Manage mime-type in database or with a better class
    }
}
