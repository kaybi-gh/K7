namespace K7.Server.Application.Features.Federation.Commands.CreateRemoteStreamSession;

public class CreateRemoteStreamSessionCommandValidator : AbstractValidator<CreateRemoteStreamSessionCommand>
{
    public CreateRemoteStreamSessionCommandValidator()
    {
        RuleFor(x => x.Request).NotNull();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
