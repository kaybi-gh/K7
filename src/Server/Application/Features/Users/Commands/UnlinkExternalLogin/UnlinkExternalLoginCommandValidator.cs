namespace K7.Server.Application.Features.Users.Commands.UnlinkExternalLogin;

public class UnlinkExternalLoginCommandValidator : AbstractValidator<UnlinkExternalLoginCommand>
{
    public UnlinkExternalLoginCommandValidator()
    {
        RuleFor(x => x.Provider).NotEmpty().MaximumLength(100);
    }
}
