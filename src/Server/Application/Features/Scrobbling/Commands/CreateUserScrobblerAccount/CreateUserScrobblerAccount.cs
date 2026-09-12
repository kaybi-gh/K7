using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Entities.Notifications;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Commands.CreateUserScrobblerAccount;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record CreateUserScrobblerAccountCommand(CreateUserScrobblerAccountRequest Request) : IRequest<Guid>;

public class CreateUserScrobblerAccountCommandHandler(
    IApplicationDbContext context,
    IUser user,
    IScrobbleConfigProtector protector)
    : IRequestHandler<CreateUserScrobblerAccountCommand, Guid>
{
    public async Task<Guid> Handle(CreateUserScrobblerAccountCommand request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<ScrobblerProvider>(request.Request.Provider, out var provider))
            throw new ArgumentException("Unknown scrobbler provider");

        var config = BuildConfig(provider, request.Request);
        var entity = new UserScrobblerAccount
        {
            UserId = user.Id!.Value,
            Provider = provider,
            IsEnabled = true,
            ConfigJson = protector.Protect(config),
            MediaTypes = ScrobblerAccountMapper.NormalizeMediaTypes(
                provider, request.Request.MediaTypes, request.Request.PresetId),
            IncludeNowPlaying = request.Request.IncludeNowPlaying,
            DisplayName = request.Request.DisplayName
        };

        context.UserScrobblerAccounts.Add(entity);
        await context.SaveChangesAsync(cancellationToken);
        return entity.Id;
    }

    private static string BuildConfig(ScrobblerProvider provider, CreateUserScrobblerAccountRequest request) =>
        provider switch
        {
            ScrobblerProvider.ListenBrainz => JsonSerializer.Serialize(new { token = request.Token }),
            ScrobblerProvider.Webhook => JsonSerializer.Serialize(new
            {
                url = request.Url,
                method = request.Method ?? "POST",
                presetId = request.PresetId,
                events = ScrobbleWebhookEvents.ForStorage(request.WebhookEvents),
                playTemplate = request.PlayTemplate,
                pauseTemplate = request.PauseTemplate,
                stopTemplate = request.StopTemplate,
                progressTemplate = request.ProgressTemplate,
                scrobbleTemplate = request.ScrobbleTemplate
            }),
            _ => throw new ArgumentException("Use the Connect flow for this provider")
        };
}
