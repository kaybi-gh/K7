namespace K7.Server.Application.Features.Downloads.Commands.DeleteDownload;

public class DeleteDownloadCommandValidator : AbstractValidator<DeleteDownloadCommand>
{
    public DeleteDownloadCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
