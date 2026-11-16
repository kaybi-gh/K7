using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Settings;
using K7.Server.Domain.Constants;
using K7.Shared.Dtos;

namespace K7.Server.Application.Features.ServerSettings.Commands.UpdateServerFeatureFlags;

[Authorize(Roles = Roles.Administrator)]
public record UpdateServerFeatureFlagsCommand : IRequest
{
    public required ServerFeatureFlagsDto Flags { get; init; }
}

public class UpdateServerFeatureFlagsCommandHandler(
    IServerSettingsService serverSettingsService,
    IShareAccessInvalidator shareAccessInvalidator)
    : IRequestHandler<UpdateServerFeatureFlagsCommand>
{
    public async Task Handle(UpdateServerFeatureFlagsCommand request, CancellationToken cancellationToken)
    {
        await serverSettingsService.SetAsync(ApplicationSettingKeys.FeatureFlags, request.Flags, cancellationToken);

        // Turning public shares off must cut visitors at once.
        shareAccessInvalidator.Invalidate();
    }
}
