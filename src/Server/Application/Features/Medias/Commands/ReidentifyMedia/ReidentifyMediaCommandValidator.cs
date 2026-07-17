namespace K7.Server.Application.Features.Medias.Commands.ReidentifyMedia;

public class ReidentifyMediaCommandValidator : AbstractValidator<ReidentifyMediaCommand>
{
    public ReidentifyMediaCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.SelectedProvider).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SelectedExternalId).NotEmpty().MaximumLength(500);
    }
}
