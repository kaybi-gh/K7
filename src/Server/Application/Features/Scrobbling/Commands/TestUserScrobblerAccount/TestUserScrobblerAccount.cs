using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Commands.TestUserScrobblerAccount;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record TestUserScrobblerAccountCommand(Guid Id) : IRequest<ScrobbleTestResultDto>;

public class TestUserScrobblerAccountCommandHandler(
    IApplicationDbContext context,
    IUser user,
    IScrobbleConfigProtector protector,
    IEnumerable<IScrobbleDestination> destinations)
    : IRequestHandler<TestUserScrobblerAccountCommand, ScrobbleTestResultDto>
{
    public async Task<ScrobbleTestResultDto> Handle(
        TestUserScrobblerAccountCommand request,
        CancellationToken cancellationToken)
    {
        var entity = await context.UserScrobblerAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == user.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        var destination = destinations.FirstOrDefault(d => d.Provider == entity.Provider);
        if (destination is null)
            return new ScrobbleTestResultDto { Success = false, Error = "Provider is not available" };

        try
        {
            var config = protector.Unprotect(entity.ConfigJson);
            var result = await destination.TestAsync(entity, config, cancellationToken);
            return new ScrobbleTestResultDto
            {
                Success = result.Success,
                Error = result.Error
            };
        }
        catch (Exception ex)
        {
            return new ScrobbleTestResultDto { Success = false, Error = ex.Message };
        }
    }
}
