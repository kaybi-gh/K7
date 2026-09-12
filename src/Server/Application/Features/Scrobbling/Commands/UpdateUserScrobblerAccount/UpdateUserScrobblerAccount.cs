using System.Text.Json;
using System.Text.Json.Nodes;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Enums;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Commands.UpdateUserScrobblerAccount;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record UpdateUserScrobblerAccountCommand(Guid Id, UpdateUserScrobblerAccountRequest Request) : IRequest;

public class UpdateUserScrobblerAccountCommandHandler(
    IApplicationDbContext context,
    IUser user,
    IScrobbleConfigProtector protector)
    : IRequestHandler<UpdateUserScrobblerAccountCommand>
{
    public async Task Handle(UpdateUserScrobblerAccountCommand request, CancellationToken cancellationToken)
    {
        var entity = await context.UserScrobblerAccounts
            .FirstOrDefaultAsync(a => a.Id == request.Id && a.UserId == user.Id, cancellationToken);

        Guard.Against.NotFound(request.Id, entity);

        entity.IsEnabled = request.Request.IsEnabled;
        entity.IncludeNowPlaying = request.Request.IncludeNowPlaying;
        entity.DisplayName = request.Request.DisplayName;

        var unprotected = entity.Provider is ScrobblerProvider.ListenBrainz or ScrobblerProvider.Webhook
            ? protector.Unprotect(entity.ConfigJson)
            : null;
        var webhookPresetId = request.Request.PresetId
            ?? ScrobblerAccountMapper.ReadWebhookPresetId(unprotected);
        entity.MediaTypes = ScrobblerAccountMapper.NormalizeMediaTypes(
            entity.Provider, request.Request.MediaTypes, webhookPresetId);

        if (entity.Provider is ScrobblerProvider.ListenBrainz or ScrobblerProvider.Webhook)
            entity.ConfigJson = protector.Protect(MergeConfig(unprotected, entity.Provider, request.Request));

        await context.SaveChangesAsync(cancellationToken);
    }

    private static string MergeConfig(
        string? unprotectedConfigJson,
        ScrobblerProvider provider,
        UpdateUserScrobblerAccountRequest request)
    {
        JsonObject root;
        try
        {
            root = JsonNode.Parse(unprotectedConfigJson ?? "{}") as JsonObject
                ?? new JsonObject();
        }
        catch
        {
            root = new JsonObject();
        }

        if (provider == ScrobblerProvider.ListenBrainz
            && !string.IsNullOrWhiteSpace(request.Token))
        {
            root["token"] = request.Token.Trim();
        }

        if (provider == ScrobblerProvider.Webhook)
        {
            if (request.Url is not null)
                root["url"] = request.Url.Trim();
            if (request.Method is not null)
                root["method"] = string.IsNullOrWhiteSpace(request.Method) ? "POST" : request.Method.Trim();
            if (request.PresetId is not null)
                root["presetId"] = request.PresetId;
            if (request.WebhookEvents is not null)
            {
                root["events"] = new JsonArray(
                    ScrobbleWebhookEvents.ForStorage(request.WebhookEvents)
                        .Select(e => JsonValue.Create(e))
                        .ToArray());
            }
            if (request.PlayTemplate is not null)
                root["playTemplate"] = request.PlayTemplate;
            if (request.PauseTemplate is not null)
                root["pauseTemplate"] = request.PauseTemplate;
            if (request.StopTemplate is not null)
                root["stopTemplate"] = request.StopTemplate;
            if (request.ProgressTemplate is not null)
                root["progressTemplate"] = request.ProgressTemplate;
            if (request.ScrobbleTemplate is not null)
                root["scrobbleTemplate"] = request.ScrobbleTemplate;
        }

        return root.ToJsonString();
    }
}
