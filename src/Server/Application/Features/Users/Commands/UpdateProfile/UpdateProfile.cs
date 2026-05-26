using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Users.Commands.UpdateProfile;

public record UpdateProfileCommand : IRequest
{
    public string? DisplayName { get; init; }
}

public class UpdateProfileCommandHandler(
    IApplicationDbContext context,
    IUser currentUser) : IRequestHandler<UpdateProfileCommand>
{
    public async Task Handle(UpdateProfileCommand request, CancellationToken cancellationToken)
    {
        Guard.Against.Null(currentUser.Id);

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == currentUser.Id, cancellationToken);

        Guard.Against.NotFound(currentUser.Id.Value, user);

        user.DisplayName = request.DisplayName?.Trim();

        await context.SaveChangesAsync(cancellationToken);
    }
}
