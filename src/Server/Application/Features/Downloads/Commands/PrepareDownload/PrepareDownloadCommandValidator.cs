namespace K7.Server.Application.Features.Downloads.Commands.PrepareDownload;

public class PrepareDownloadCommandValidator : AbstractValidator<PrepareDownloadCommand>
{
    public PrepareDownloadCommandValidator()
    {
        RuleFor(v => v.IndexedFileId)
            .NotEmpty();

        RuleFor(v => v.DeviceId)
            .NotEmpty();

        RuleFor(v => v.AudioTrackIndex)
            .GreaterThanOrEqualTo(0)
            .When(v => v.AudioTrackIndex is not null);

        RuleForEach(v => v.SubtitleTrackIndices)
            .GreaterThanOrEqualTo(0)
            .When(v => v.SubtitleTrackIndices is not null);
    }
}
