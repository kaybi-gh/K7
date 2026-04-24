using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Entities;
using Microsoft.EntityFrameworkCore;

namespace K7.Server.Application.Features.Libraries.Queries.GetLibraryPictures;

public record GetLibraryPicturesQuery(Guid LibraryId) : IRequest<IEnumerable<LibraryPictureDto>>;

public class GetLibraryPicturesQueryHandler(IApplicationDbContext context)
    : IRequestHandler<GetLibraryPicturesQuery, IEnumerable<LibraryPictureDto>>
{
    public async Task<IEnumerable<LibraryPictureDto>> Handle(GetLibraryPicturesQuery request, CancellationToken cancellationToken)
    {
        var mediaIds = await context.IndexedFiles
            .Where(f => f.LibraryId == request.LibraryId && f.MediaId != null)
            .Select(f => f.MediaId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await context.MetadataPictures
            .AsNoTracking()
            .Where(p =>
                p.MediaId != null &&
                mediaIds.Contains(p.MediaId.Value) &&
                (p.Type == MetadataPictureType.Poster ||
                 p.Type == MetadataPictureType.Backdrop ||
                 p.Type == MetadataPictureType.Cover) &&
                p.LocalPath != null)
            .OrderBy(p => p.Type)
            .Take(100)
            .Select(p => new LibraryPictureDto { Id = p.Id, Type = p.Type, DominantColor = p.DominantColor })
            .ToListAsync(cancellationToken);
    }
}
