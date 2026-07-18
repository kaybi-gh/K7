using FluentValidation;

namespace K7.Server.Application.Features.Medias.Commands.ExtractSerieThemeSong;

public class ExtractSerieThemeSongCommandValidator : AbstractValidator<ExtractSerieThemeSongCommand>
{
    public ExtractSerieThemeSongCommandValidator()
    {
        RuleFor(x => x.SerieId).NotEmpty();
    }
}
