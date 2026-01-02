using K7.Server.Application.Common.Interfaces;

namespace K7.Server.Application.Features.Devices.Commands.CreateDevice;

public class CreateDeviceCommandValidator : AbstractValidator<CreateDeviceCommand>
{
    private readonly IApplicationDbContext _context;

    public CreateDeviceCommandValidator(IApplicationDbContext context)
    {
        _context = context;

    }
}
