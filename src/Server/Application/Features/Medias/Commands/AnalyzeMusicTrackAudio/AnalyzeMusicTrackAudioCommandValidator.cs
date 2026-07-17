namespace K7.Server.Application.Features.Medias.Commands.AnalyzeMusicTrackAudio;

public class AnalyzeMusicTrackAudioCommandValidator : AbstractValidator<AnalyzeMusicTrackAudioCommand>
{
    public AnalyzeMusicTrackAudioCommandValidator()
    {
        RuleFor(x => x.TrackId).NotEmpty();
    }
}
