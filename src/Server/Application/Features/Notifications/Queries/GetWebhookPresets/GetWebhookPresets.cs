using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Notifications.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Notifications;

namespace K7.Server.Application.Features.Notifications.Queries.GetWebhookPresets;

[Authorize(Roles = Roles.Administrator)]
public record GetWebhookPresetsQuery : IRequest<IReadOnlyList<NotificationWebhookPresetDto>>;

public class GetWebhookPresetsQueryHandler : IRequestHandler<GetWebhookPresetsQuery, IReadOnlyList<NotificationWebhookPresetDto>>
{
    public Task<IReadOnlyList<NotificationWebhookPresetDto>> Handle(
        GetWebhookPresetsQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult(NotificationWebhookPresets.All);
}
