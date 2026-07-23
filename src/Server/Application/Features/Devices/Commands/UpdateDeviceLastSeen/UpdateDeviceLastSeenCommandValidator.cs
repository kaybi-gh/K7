namespace K7.Server.Application.Features.Devices.Commands.UpdateDeviceLastSeen;

public class UpdateDeviceLastSeenCommandValidator : AbstractValidator<UpdateDeviceLastSeenCommand>
{
    public UpdateDeviceLastSeenCommandValidator()
    {
        RuleFor(x => x.DeviceId).NotEmpty();
    }
}
