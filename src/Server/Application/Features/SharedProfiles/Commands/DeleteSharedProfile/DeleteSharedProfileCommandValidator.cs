namespace K7.Server.Application.Features.SharedProfiles.Commands.DeleteSharedProfile;

public class DeleteSharedProfileCommandValidator : AbstractValidator<DeleteSharedProfileCommand>
{
    public DeleteSharedProfileCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
