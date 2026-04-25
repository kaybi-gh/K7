using System.Text.Json;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.Home;

namespace K7.Server.Application.Features.Home.Commands.UpdateUserHomeLayout;

[Authorize]
public record UpdateUserHomeLayoutCommand : IRequest
{
    public required HomeLayoutDto Layout { get; init; }
}

public class UpdateUserHomeLayoutCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateUserHomeLayoutCommand>
{
    public async Task Handle(UpdateUserHomeLayoutCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Layout);
        await userSettingsService.SetAsync(userId, UserSettingKeys.HomeLayout, json, cancellationToken);
    }
}
