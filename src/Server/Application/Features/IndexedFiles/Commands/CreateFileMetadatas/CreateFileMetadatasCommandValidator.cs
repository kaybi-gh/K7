namespace K7.Server.Application.Features.IndexedFiles.Commands.CreateFileMetadatas;

public class CreateFileMetadatasCommandValidator : AbstractValidator<CreateFileMetadatasCommand>
{
    public CreateFileMetadatasCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.FileType).IsInEnum();
    }
}
