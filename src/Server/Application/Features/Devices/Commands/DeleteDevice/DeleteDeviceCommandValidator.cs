namespace K7.Server.Application.Features.Devices.Commands.DeleteDevice;

public class DeleteDeviceCommandValidator : AbstractValidator<DeleteDeviceCommand>
{
    public DeleteDeviceCommandValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
    }
}
