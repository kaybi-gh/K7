namespace K7.Server.Application.Features.IndexedFiles.Commands.ComputeHlsSegments;

public class ComputeHlsSegmentsCommandValidator : AbstractValidator<ComputeHlsSegmentsCommand>
{
    public ComputeHlsSegmentsCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.SegmentsDuration).GreaterThan(TimeSpan.Zero);
    }
}
