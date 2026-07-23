namespace K7.Server.Application.Features.Devices.Commands.BulkResolveImportDevices;

public class BulkResolveImportDevicesCommandValidator : AbstractValidator<BulkResolveImportDevicesCommand>
{
    public BulkResolveImportDevicesCommandValidator()
    {
        RuleFor(x => x.Items).NotNull();
    }
}
