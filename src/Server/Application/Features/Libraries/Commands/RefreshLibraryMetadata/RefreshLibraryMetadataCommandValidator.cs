namespace K7.Server.Application.Features.Libraries.Commands.RefreshLibraryMetadata;

public class RefreshLibraryMetadataCommandValidator : AbstractValidator<RefreshLibraryMetadataCommand>
{
    public RefreshLibraryMetadataCommandValidator()
    {
        RuleFor(x => x.LibraryId).NotEmpty();
    }
}
