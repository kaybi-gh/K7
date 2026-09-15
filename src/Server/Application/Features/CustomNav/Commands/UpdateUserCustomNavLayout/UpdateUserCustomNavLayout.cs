using System.Text.Json;
using K7.Server.Application.Common.Interfaces;
using K7.Server.Application.Common.Security;
using K7.Server.Application.Features.CustomNav;
using K7.Server.Domain.Settings;
using K7.Shared.Dtos.CustomNav;

namespace K7.Server.Application.Features.CustomNav.Commands.UpdateUserCustomNavLayout;

[Authorize]
public record UpdateUserCustomNavLayoutCommand : IRequest
{
    public required CustomNavLayoutDto Layout { get; init; }
}

public class UpdateUserCustomNavLayoutCommandHandler(IUserSettingsService userSettingsService, IUser currentUser)
    : IRequestHandler<UpdateUserCustomNavLayoutCommand>
{
    public async Task Handle(UpdateUserCustomNavLayoutCommand request, CancellationToken cancellationToken)
    {
        var userId = Guard.Against.Null(currentUser.Id);
        var json = JsonSerializer.Serialize(request.Layout);
        await userSettingsService.SetAsync(userId, UserSettingKeys.CustomNavLayout, json, cancellationToken);
    }
}

public class UpdateUserCustomNavLayoutCommandValidator : AbstractValidator<UpdateUserCustomNavLayoutCommand>
{
    public UpdateUserCustomNavLayoutCommandValidator()
    {
        RuleFor(v => v.Layout)
            .NotNull()
            .SetValidator(new CustomNavLayoutValidator());
    }
}
