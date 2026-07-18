namespace K7.Server.Application.Features.Medias.Commands.UpdatePlaybackProgress;

public class UpdatePlaybackProgressCommandValidator : AbstractValidator<UpdatePlaybackProgressCommand>
{
    public UpdatePlaybackProgressCommandValidator()
    {
        RuleFor(v => v.MediaId)
            .NotEmpty();

        RuleFor(v => v.SessionId)
            .NotEmpty();

        RuleFor(v => v.ReferenceId)
            .NotEmpty();

        RuleFor(v => v.Position)
            .GreaterThanOrEqualTo(0);

        RuleFor(v => v.Duration)
            .GreaterThanOrEqualTo(0);

        RuleFor(v => v.State)
            .IsInEnum();

        RuleFor(v => v.DeviceId)
            .NotEmpty()
            .When(v => v.DeviceId is not null);

        RuleFor(v => v.PlaylistId)
            .NotEmpty()
            .When(v => v.PlaylistId is not null);

        RuleFor(v => v.SharedProfileId)
            .NotEmpty()
            .When(v => v.SharedProfileId is not null);

        RuleFor(v => v.SyncPlayGroupId)
            .NotEmpty()
            .When(v => v.SyncPlayGroupId is not null);
    }
}
