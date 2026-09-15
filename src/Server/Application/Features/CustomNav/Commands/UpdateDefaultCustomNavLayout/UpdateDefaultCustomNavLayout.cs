using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.CustomNav;
using K7.Server.Domain.Constants;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.CustomNav;

namespace K7.Server.Application.Features.CustomNav.Commands.UpdateDefaultCustomNavLayout;

[Authorize(Roles = Roles.Administrator)]
public record UpdateDefaultCustomNavLayoutCommand : IRequest
{
    public required CustomNavLayoutDto Layout { get; init; }
}

public class UpdateDefaultCustomNavLayoutCommandHandler(IServerSettingsService serverSettingsService)
    : IRequestHandler<UpdateDefaultCustomNavLayoutCommand>
{
    public async Task Handle(UpdateDefaultCustomNavLayoutCommand request, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(request.Layout);
        await serverSettingsService.SetAsync(ServerSettingKeys.CustomNavLayout, json, cancellationToken);
    }
}

public class UpdateDefaultCustomNavLayoutCommandValidator : AbstractValidator<UpdateDefaultCustomNavLayoutCommand>
{
    public UpdateDefaultCustomNavLayoutCommandValidator()
    {
        RuleFor(v => v.Layout)
            .NotNull()
            .SetValidator(new CustomNavLayoutValidator());
    }
}
