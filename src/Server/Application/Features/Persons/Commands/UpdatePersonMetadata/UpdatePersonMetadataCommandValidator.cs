using FluentValidation;

namespace K7.Server.Application.Features.Persons.Commands.UpdatePersonMetadata;

public class UpdatePersonMetadataCommandValidator : AbstractValidator<UpdatePersonMetadataCommand>
{
    public UpdatePersonMetadataCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();
        RuleFor(v => v.LockedFields).NotNull();
        RuleFor(v => v.Name).NotEmpty().MaximumLength(500).When(v => v.Name is not null);
        RuleFor(v => v.Biography).MaximumLength(50000).When(v => v.Biography is not null);
        RuleFor(v => v.BirthPlace).MaximumLength(200).When(v => v.BirthPlace is not null);
    }
}
