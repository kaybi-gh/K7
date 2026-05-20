using FluentValidation;

namespace K7.Server.Application.Features.Medias.Commands.UpdateMediaMetadata;

public class UpdateMediaMetadataCommandValidator : AbstractValidator<UpdateMediaMetadataCommand>
{
    public UpdateMediaMetadataCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.LockedFields).NotNull();
        RuleFor(v => v.Title).MaximumLength(500).When(v => v.Title is not null);
        RuleFor(v => v.OriginalTitle).MaximumLength(500).When(v => v.OriginalTitle is not null);
        RuleFor(v => v.Tagline).MaximumLength(1000).When(v => v.Tagline is not null);
        RuleFor(v => v.Overview).MaximumLength(10000).When(v => v.Overview is not null);
        RuleFor(v => v.OriginalLanguage).MaximumLength(10).When(v => v.OriginalLanguage is not null);
        RuleFor(v => v.ContentRating).MaximumLength(50).When(v => v.ContentRating is not null);
        RuleFor(v => v.Status).MaximumLength(50).When(v => v.Status is not null);
        RuleFor(v => v.Network).MaximumLength(200).When(v => v.Network is not null);
        RuleFor(v => v.Biography).MaximumLength(50000).When(v => v.Biography is not null);
        RuleFor(v => v.Country).MaximumLength(100).When(v => v.Country is not null);
    }
}
