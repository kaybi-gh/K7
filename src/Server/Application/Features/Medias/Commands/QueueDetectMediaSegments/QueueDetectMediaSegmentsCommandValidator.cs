namespace K7.Server.Application.Features.Medias.Commands.QueueDetectMediaSegments;

public class QueueDetectMediaSegmentsCommandValidator : AbstractValidator<QueueDetectMediaSegmentsCommand>
{
    public QueueDetectMediaSegmentsCommandValidator()
    {
        RuleFor(x => x.SeasonId).NotEmpty();
    }
}
