namespace K7.Server.Application.Features.Devices.Commands.AttachDeviceToCurrentUser;

public class AttachDeviceToCurrentUserCommandValidator : AbstractValidator<AttachDeviceToCurrentUserCommand>
{
    public AttachDeviceToCurrentUserCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
    }
}
