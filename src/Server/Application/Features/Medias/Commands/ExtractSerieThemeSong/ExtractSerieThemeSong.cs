using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Medias.Commands.ExtractSerieThemeSong;

public record ExtractSerieThemeSongCommand : IRequest
{
    public required Guid SerieId { get; init; }
}

public class ExtractSerieThemeSongCommandHandler(IThemeSongService themeSongService)
    : IRequestHandler<ExtractSerieThemeSongCommand>
{
    public Task Handle(ExtractSerieThemeSongCommand request, CancellationToken cancellationToken) =>
        themeSongService.ExtractSerieThemeAsync(request.SerieId, cancellationToken);
}
