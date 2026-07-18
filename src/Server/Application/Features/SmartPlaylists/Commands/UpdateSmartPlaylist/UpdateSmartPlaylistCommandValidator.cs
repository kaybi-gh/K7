namespace K7.Server.Application.Features.SmartPlaylists.Commands.UpdateSmartPlaylist;

public class UpdateSmartPlaylistCommandValidator : AbstractValidator<UpdateSmartPlaylistCommand>
{
    public UpdateSmartPlaylistCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();

        RuleFor(v => v.Title)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(v => v.Description)
            .MaximumLength(2000)
            .When(v => v.Description is not null);

        RuleFor(v => v.MediaType)
            .IsInEnum();

        RuleFor(v => v.OrderBy)
            .IsInEnum();

        RuleFor(v => v.Limit)
            .GreaterThan(0)
            .LessThanOrEqualTo(1000)
            .When(v => v.Limit is not null);

        RuleFor(v => v.RuleFilter)
            .NotNull();
    }
}
