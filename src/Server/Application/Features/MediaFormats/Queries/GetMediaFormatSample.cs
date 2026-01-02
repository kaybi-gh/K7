using K7.Server.Application.Helpers;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Interfaces;
using Microsoft.AspNetCore.Http;

namespace K7.Server.Application.Features.MediaFormatSample.Queries.GetMediaFormatSample;

public record GetMediaFormatSampleQuery(string Id) : IRequest<IResult>;

public class GetMediaFormatSampleQueryHandler(IMediaFormatSampleGenerator mediaFormatSampleGenerationService)
    : IRequestHandler<GetMediaFormatSampleQuery, IResult>
{
    public async Task<IResult> Handle(GetMediaFormatSampleQuery request, CancellationToken cancellationToken)
    {
        var mediaFormat = Constants.MediaFormats.FirstOrDefault(x => x.Id == request.Id);

        if (mediaFormat == null)
        {
            return Results.NotFound(request.Id);
        }

        var sample = await mediaFormatSampleGenerationService.GenerateSampleAsync(mediaFormat);
        return Results.File(sample,
            contentType: MimeTypeHelper.GetMimeType(mediaFormat.Type, mediaFormat.Container));
    }
}

