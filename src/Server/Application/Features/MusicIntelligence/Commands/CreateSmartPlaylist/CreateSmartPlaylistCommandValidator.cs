namespace K7.Server.Application.Features.MusicIntelligence.Commands.CreateSmartPlaylist;

public class CreateSmartPlaylistCommandValidator : AbstractValidator<CreateSmartPlaylistCommand>
{
    public CreateSmartPlaylistCommandValidator()
    {
        RuleFor(x => x.Prompt).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Count).GreaterThan(0);
    }
}
