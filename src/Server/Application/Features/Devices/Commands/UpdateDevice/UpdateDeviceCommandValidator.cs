using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Devices.Commands.UpdateDevice;

public class UpdateDeviceCommandValidator : AbstractValidator<UpdateDeviceCommand>
{
    private readonly IApplicationDbContext _context;

    public UpdateDeviceCommandValidator(IApplicationDbContext context)
    {
        _context = context;

    }
}
