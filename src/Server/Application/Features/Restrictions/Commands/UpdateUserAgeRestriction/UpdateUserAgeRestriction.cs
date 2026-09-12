using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Restrictions;

namespace K7.Server.Application.Features.Restrictions.Commands.UpdateUserAgeRestriction;

[Authorize(Roles = Roles.Administrator)]
public record UpdateUserAgeRestrictionCommand : IRequest
{
    public required Guid UserId { get; init; }
    public required bool Enabled { get; init; }
    public DateOnly? DateOfBirth { get; init; }
    public bool HideUnratedTitles { get; init; } = true;
}

public class UpdateUserAgeRestrictionCommandHandler(IApplicationDbContext context)
    : IRequestHandler<UpdateUserAgeRestrictionCommand>
{
    public async Task Handle(UpdateUserAgeRestrictionCommand request, CancellationToken cancellationToken)
    {
        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == request.UserId, cancellationToken);

        Guard.Against.NotFound(request.UserId, user);

        user.AgeRestrictionEnabled = request.Enabled;
        user.DateOfBirth = request.DateOfBirth;
        user.HideUnratedTitles = request.HideUnratedTitles;
        await context.SaveChangesAsync(cancellationToken);
    }
}

public static class UpdateUserAgeRestrictionCommandExtensions
{
    public static UpdateUserAgeRestrictionCommand ToCommand(this UpdateAgeRestrictionRequest request, Guid userId) =>
        new()
        {
            UserId = userId,
            Enabled = request.Enabled,
            DateOfBirth = request.DateOfBirth,
            HideUnratedTitles = request.HideUnratedTitles
        };
}
