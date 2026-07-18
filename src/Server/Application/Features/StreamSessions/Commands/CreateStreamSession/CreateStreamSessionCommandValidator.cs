namespace K7.Server.Application.Features.StreamSessions.Commands.CreateStreamSession;

public class CreateStreamSessionCommandValidator : AbstractValidator<CreateStreamSessionCommand>
{
    public CreateStreamSessionCommandValidator()
    {
        RuleFor(v => v.IndexedFileId)
            .NotEmpty();

        RuleFor(v => v.DeviceId)
            .NotEmpty();

        RuleFor(v => v.AudioTrackIndex)
            .GreaterThanOrEqualTo(0)
            .When(v => v.AudioTrackIndex is not null);
    }
}
