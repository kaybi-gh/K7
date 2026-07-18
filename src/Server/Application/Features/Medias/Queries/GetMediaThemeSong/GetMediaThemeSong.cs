using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Services;

namespace K7.Server.Application.Features.Medias.Queries.GetMediaThemeSong;

public record GetMediaThemeSongQuery(Guid MediaId) : IRequest<GetMediaThemeSongResult?>;

public sealed record GetMediaThemeSongResult(string FilePath, string ContentType);

public class GetMediaThemeSongQueryHandler(
    IThemeSongService themeSongService,
    IMediaAccessGuard accessGuard)
    : IRequestHandler<GetMediaThemeSongQuery, GetMediaThemeSongResult?>
{
    public async Task<GetMediaThemeSongResult?> Handle(GetMediaThemeSongQuery request, CancellationToken cancellationToken)
    {
        await accessGuard.EnsureAccessAsync(request.MediaId, cancellationToken);

        var path = await themeSongService.ResolvePlayablePathAsync(request.MediaId, cancellationToken);
        if (path is null || !File.Exists(path))
            return null;

        var contentType = Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".m4a" => "audio/mp4",
            ".ogg" => "audio/ogg",
            _ => "application/octet-stream"
        };

        return new GetMediaThemeSongResult(path, contentType);
    }
}
