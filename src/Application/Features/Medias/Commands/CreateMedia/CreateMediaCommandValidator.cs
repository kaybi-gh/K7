namespace MediaServer.Application.Features.Medias.Commands.CreateMedia;

public class CreateMediaCommandValidator : AbstractValidator<CreateMediaCommand>
{
    public CreateMediaCommandValidator()
    {
        RuleFor(v => v.MediaType)
            .IsInEnum()
            .NotEmpty();

        RuleFor(v => v.IndexedFile)
            .NotNull();

        RuleFor(v => v.IndexedFile.Id)
            .NotNull();

        RuleFor(v => v.IndexedFile.Identification)
            .NotNull();
    }
}
