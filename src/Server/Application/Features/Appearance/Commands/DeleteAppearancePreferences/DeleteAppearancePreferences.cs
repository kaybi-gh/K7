using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.Appearance.Commands.DeleteAppearancePreferences;

[Authorize]
public record DeleteAppearancePreferencesCommand : IRequest;

public class DeleteAppearancePreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteAppearancePreferencesCommand>
{
    public async Task Handle(DeleteAppearancePreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.Appearance, cancellationToken);
    }
}
