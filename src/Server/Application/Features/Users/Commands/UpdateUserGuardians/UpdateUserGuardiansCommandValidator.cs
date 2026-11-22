namespace K7.Server.Application.Features.Users.Commands.UpdateUserGuardians;

public class UpdateUserGuardiansCommandValidator : AbstractValidator<UpdateUserGuardiansCommand>
{
    public UpdateUserGuardiansCommandValidator()
    {
        RuleFor(v => v.Id).NotEmpty();

        RuleFor(v => v.GuardianUserIds)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(ids => ids.All(id => id != Guid.Empty))
                .WithMessage("A guardian id is empty.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
                .WithMessage("A guardian is listed twice.");

        RuleFor(v => v)
            .Must(v => v.GuardianUserIds is null || !v.GuardianUserIds.Contains(v.Id))
                .WithName(nameof(UpdateUserGuardiansCommand.GuardianUserIds))
                .WithMessage("An account cannot be its own guardian.");
    }
}
