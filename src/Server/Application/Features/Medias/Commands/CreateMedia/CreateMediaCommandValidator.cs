namespace K7.Server.Application.Features.Medias.Commands.CreateMedia;

public class CreateMediaCommandValidator : AbstractValidator<CreateMediaCommand>
{
    public CreateMediaCommandValidator()
    {
        RuleFor(v => v.MediaType)
            .IsInEnum()
            .NotEmpty();

        RuleFor(v => v.IndexedFileId)
            .NotNull();
    }
}
