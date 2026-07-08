using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;

namespace K7.Server.Application.Features.UserSettings.Queries.UserSettingExists;

[Authorize]
public record UserSettingExistsQuery(string Key) : IRequest<bool>;

public class UserSettingExistsQueryHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UserSettingExistsQuery, bool>
{
    public async Task<bool> Handle(UserSettingExistsQuery request, CancellationToken cancellationToken)
    {
        if (currentUser.Id is not { } userId)
            return false;

        return await userSettingsService.ExistsAsync(userId, request.Key, cancellationToken);
    }
}
