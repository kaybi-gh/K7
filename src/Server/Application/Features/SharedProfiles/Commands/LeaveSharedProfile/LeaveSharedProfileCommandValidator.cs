namespace K7.Server.Application.Features.SharedProfiles.Commands.LeaveSharedProfile;

public class LeaveSharedProfileCommandValidator : AbstractValidator<LeaveSharedProfileCommand>
{
    public LeaveSharedProfileCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.NewHostUserId).NotEqual(Guid.Empty).When(x => x.NewHostUserId.HasValue);
    }
}
