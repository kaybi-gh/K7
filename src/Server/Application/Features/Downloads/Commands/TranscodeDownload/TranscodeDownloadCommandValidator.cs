namespace K7.Server.Application.Features.Downloads.Commands.TranscodeDownload;

public class TranscodeDownloadCommandValidator : AbstractValidator<TranscodeDownloadCommand>
{
    public TranscodeDownloadCommandValidator()
    {
        RuleFor(x => x.DownloadId).NotEmpty();
    }
}
