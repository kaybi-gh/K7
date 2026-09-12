using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.SharedProfiles;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Restrictions;

namespace K7.Server.Application.Features.SharedProfiles.Commands.UpdateSharedProfileAgeRestriction;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdateSharedProfileAgeRestrictionCommand : IRequest
{
    public required Guid SharedProfileId { get; init; }
    public required bool Enabled { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public bool HideUnratedTitles { get; init; } = true;
}

public class UpdateSharedProfileAgeRestrictionCommandHandler(
    IApplicationDbContext context,
    IUser currentUser,
    IIdentityService identityService)
    : IRequestHandler<UpdateSharedProfileAgeRestrictionCommand>
{
    public async Task Handle(UpdateSharedProfileAgeRestrictionCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var group = await SharedProfileMemberValidator.GetGroupForHostAsync(
            context, identityService, request.SharedProfileId, userId, currentUser.IdentityId, cancellationToken);

        group.AgeRestrictionEnabled = request.Enabled;
        group.ViewerDateOfBirth = request.DateOfBirth;
        group.HideUnratedTitles = request.HideUnratedTitles;
        await context.SaveChangesAsync(cancellationToken);
    }
}

public static class UpdateSharedProfileAgeRestrictionCommandExtensions
{
    public static UpdateSharedProfileAgeRestrictionCommand ToCommand(
        this UpdateAgeRestrictionRequest request,
        Guid sharedProfileId) =>
        new()
        {
            SharedProfileId = sharedProfileId,
            Enabled = request.Enabled,
            DateOfBirth = request.DateOfBirth,
            HideUnratedTitles = request.HideUnratedTitles
        };
}
