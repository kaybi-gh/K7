namespace K7.Server.Application.Features.Medias.Commands.DetectMediaSegments;

public class DetectMediaSegmentsCommandValidator : AbstractValidator<DetectMediaSegmentsCommand>
{
    public DetectMediaSegmentsCommandValidator()
    {
        RuleFor(x => x.SeasonId).NotEmpty();
    }
}
