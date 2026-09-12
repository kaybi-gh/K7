using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;

namespace K7.Server.Application.Features.Scrobbling.Commands.PollTraktDevice;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record PollTraktDeviceCommand(
    string DeviceCode,
    IReadOnlyList<string>? MediaTypes = null,
    string? DisplayName = null) : IRequest<Guid?>;

public class PollTraktDeviceCommandHandler(
    TraktClient traktClient,
    IApplicationDbContext context,
    IUser user,
    IScrobbleConfigProtector protector)
    : IRequestHandler<PollTraktDeviceCommand, Guid?>
{
    public async Task<Guid?> Handle(PollTraktDeviceCommand request, CancellationToken cancellationToken)
    {
        var tokens = await traktClient.PollDeviceAsync(request.DeviceCode, cancellationToken);
        if (tokens is null)
            return null;

        var entity = new UserScrobblerAccount
        {
            UserId = user.Id!.Value,
            Provider = ScrobblerProvider.Trakt,
            IsEnabled = true,
            ConfigJson = protector.Protect(JsonSerializer.Serialize(new
            {
                accessToken = tokens.Value.AccessToken,
                refreshToken = tokens.Value.RefreshToken,
                expiresAt = tokens.Value.ExpiresAt
            })),
            MediaTypes = ScrobblerAccountMapper.NormalizeMediaTypes(ScrobblerProvider.Trakt, request.MediaTypes),
            IncludeNowPlaying = true,
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? "Trakt" : request.DisplayName.Trim()
        };

        context.UserScrobblerAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }
}
