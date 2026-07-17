namespace K7.Server.Application.Features.Users.Commands.UpdateUserCapabilities;

public class UpdateUserCapabilitiesCommandValidator : AbstractValidator<UpdateUserCapabilitiesCommand>
{
    public UpdateUserCapabilitiesCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Overrides).NotNull();
    }
}
