namespace K7.Server.Application.Features.ServerSettings.Commands.UpdateServerFeatureFlags;

public class UpdateServerFeatureFlagsCommandValidator : AbstractValidator<UpdateServerFeatureFlagsCommand>
{
    public UpdateServerFeatureFlagsCommandValidator()
    {
        RuleFor(x => x.Flags).NotNull();
    }
}
