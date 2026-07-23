namespace K7.Server.Application.Features.IndexedFiles.Commands.ReidentifyIndexedFile;

public class ReidentifyIndexedFileCommandValidator : AbstractValidator<ReidentifyIndexedFileCommand>
{
    public ReidentifyIndexedFileCommandValidator()
    {
        RuleFor(x => x.IndexedFileId).NotEmpty();
        RuleFor(x => x.SelectedProvider).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SelectedExternalId).NotEmpty().MaximumLength(500);
    }
}
