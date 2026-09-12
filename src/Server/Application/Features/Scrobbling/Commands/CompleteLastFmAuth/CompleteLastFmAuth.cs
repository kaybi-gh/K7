using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Scrobbling.Commands.CompleteLastFmAuth;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CompleteLastFmAuthCommand(string Token, IReadOnlyList<string>? MediaTypes = null) : IRequest<Guid>;

public class CompleteLastFmAuthCommandHandler(
    LastFmClient lastFmClient,
    IApplicationDbContext context,
    IUser user,
    IScrobbleConfigProtector protector)
    : IRequestHandler<CompleteLastFmAuthCommand, Guid>
{
    public async Task<Guid> Handle(CompleteLastFmAuthCommand request, CancellationToken cancellationToken)
    {
        var (sessionKey, username) = await lastFmClient.GetSessionAsync(request.Token, cancellationToken);
        var entity = new UserScrobblerAccount
        {
            UserId = user.Id!.Value,
            Provider = ScrobblerProvider.LastFm,
            IsEnabled = true,
            ConfigJson = protector.Protect(JsonSerializer.Serialize(new { sessionKey, username })),
            MediaTypes = ScrobblerAccountMapper.NormalizeMediaTypes(ScrobblerProvider.LastFm, request.MediaTypes),
            IncludeNowPlaying = true,
            DisplayName = username
        };

        context.UserScrobblerAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
