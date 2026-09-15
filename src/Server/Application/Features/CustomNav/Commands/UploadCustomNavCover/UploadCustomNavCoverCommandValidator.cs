namespace K7.Server.Application.Features.CustomNav.Commands.UploadCustomNavCover;

public class UploadCustomNavCoverCommandValidator : AbstractValidator<UploadCustomNavCoverCommand>
{
    public UploadCustomNavCoverCommandValidator()
    {
        RuleFor(v => v.ItemId).NotEmpty();
        RuleFor(v => v.FileName).MaximumLength(500);
        RuleFor(v => v)
            .Must(v => (v.FileStream is not null && !string.IsNullOrWhiteSpace(v.FileName))
                || v.SourcePictureId is not null)
            .WithMessage("Either a file or a source picture is required.");
    }
}
