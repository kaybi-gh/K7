using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.Scrobbling.Services;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos.Scrobbling;

namespace K7.Server.Application.Features.Scrobbling.Queries.GetScrobbleWebhookPresets;

[Authorize(Roles = $"{Roles.User},{Roles.Administrator}")]
public record GetScrobbleWebhookPresetsQuery : IRequest<IReadOnlyList<ScrobbleWebhookPresetDto>>;

public class GetScrobbleWebhookPresetsQueryHandler
    : IRequestHandler<GetScrobbleWebhookPresetsQuery, IReadOnlyList<ScrobbleWebhookPresetDto>>
{
    public Task<IReadOnlyList<ScrobbleWebhookPresetDto>> Handle(
        GetScrobbleWebhookPresetsQuery request,
        CancellationToken cancellationToken) =>
        Task.FromResult(ScrobbleWebhookPresets.All);
}
