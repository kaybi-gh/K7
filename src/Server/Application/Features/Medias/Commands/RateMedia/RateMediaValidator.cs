namespace K7.Server.Application.Features.Medias.Commands.RateMedia;

public class RateMediaCommandValidator : AbstractValidator<RateMediaCommand>
{
    public RateMediaCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.Value).InclusiveBetween(0, 10);
    }
}
