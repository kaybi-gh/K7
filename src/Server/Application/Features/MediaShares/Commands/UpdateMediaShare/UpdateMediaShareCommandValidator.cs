using K7.Server.Application.Features.MediaShares.Services;

namespace K7.Server.Application.Features.MediaShares.Commands.UpdateMediaShare;

public class UpdateMediaShareCommandValidator : AbstractValidator<UpdateMediaShareCommand>
{
    public UpdateMediaShareCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Label).MaximumLength(MediaShareRules.MaxLabelLength);
        RuleFor(x => x.Password)
            .MinimumLength(MediaShareRules.MinPasswordLength)
            .MaximumLength(MediaShareRules.MaxPasswordLength)
            .When(x => !string.IsNullOrEmpty(x.Password));
        RuleFor(x => x.MaxAccessCount)
            .InclusiveBetween(1, MediaShareRules.MaxAccessCountLimit)
            .When(x => x.MaxAccessCount is not null);
        RuleFor(x => x)
            .Must(x => x.AllowPlayback || x.AllowDownload)
            .WithName(nameof(UpdateMediaShareCommand.AllowPlayback))
            .WithMessage("A share must allow playback or download.");
    }
}
