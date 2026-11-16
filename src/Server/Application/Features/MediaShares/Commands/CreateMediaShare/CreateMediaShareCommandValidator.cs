using K7.Server.Application.Features.MediaShares.Services;

namespace K7.Server.Application.Features.MediaShares.Commands.CreateMediaShare;

public class CreateMediaShareCommandValidator : AbstractValidator<CreateMediaShareCommand>
{
    public CreateMediaShareCommandValidator()
    {
        RuleFor(x => x.MediaId).NotEmpty();
        RuleFor(x => x.Label).MaximumLength(MediaShareRules.MaxLabelLength);
        RuleFor(x => x.Password)
            .MinimumLength(MediaShareRules.MinPasswordLength)
            .MaximumLength(MediaShareRules.MaxPasswordLength)
            .When(x => !string.IsNullOrEmpty(x.Password));
        RuleFor(x => x.MaxAccessCount)
            .InclusiveBetween(1, MediaShareRules.MaxAccessCountLimit)
            .When(x => x.MaxAccessCount is not null);
        RuleFor(x => x.ExpiresAt)
            .Must(expiresAt => expiresAt > DateTimeOffset.UtcNow)
            .When(x => x.ExpiresAt is not null)
            .WithMessage("The expiration must be in the future.");
        RuleFor(x => x)
            .Must(x => x.AllowPlayback || x.AllowDownload)
            .WithName(nameof(CreateMediaShareCommand.AllowPlayback))
            .WithMessage("A share must allow playback or download.");
    }
}
