using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Domain.Settings;

namespace K7.Server.Application.Features.GeneralPreferences.Commands.DeleteUserGeneralPreferences;

[Authorize]
public record DeleteUserGeneralPreferencesCommand : IRequest;

public class DeleteUserGeneralPreferencesCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<DeleteUserGeneralPreferencesCommand>
{
    public async Task Handle(DeleteUserGeneralPreferencesCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        await userSettingsService.RemoveAsync(userId, UserSettingKeys.GeneralPreferences, cancellationToken);
    }
}
